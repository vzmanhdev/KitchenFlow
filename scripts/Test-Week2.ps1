param([string]$Project = "kitchenflow-test-$([guid]::NewGuid().ToString('N').Substring(0, 8))")

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$env:POSTGRES_PASSWORD = [guid]::NewGuid().ToString('N')
$env:RABBITMQ_PASSWORD = [guid]::NewGuid().ToString('N')
$env:RABBITMQ_USER = 'kitchenflow'
$env:POSTGRES_PORT = '55432'
$env:RABBITMQ_PORT = '55672'
$env:RABBITMQ_MANAGEMENT_PORT = '55673'
$env:ORDER_API_PORT = '55080'
$env:KITCHEN_COOKING_SECONDS = '4'
$env:ConnectionStrings__Orders = "Host=127.0.0.1;Port=$env:POSTGRES_PORT;Database=kitchenflow;Username=kitchenflow;Password=$env:POSTGRES_PASSWORD"
$api = "http://127.0.0.1:$env:ORDER_API_PORT"
$management = "http://127.0.0.1:$env:RABBITMQ_MANAGEMENT_PORT/api"
$credentials = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("kitchenflow:$env:RABBITMQ_PASSWORD"))
$headers = @{ Authorization = "Basic $credentials" }

function Compose { docker compose -p $Project --profile apps @args }
function Check($Condition, $Message) { if (!$Condition) { throw $Message } }
function Wait-Until([scriptblock]$Condition, [string]$Message, [int]$Seconds = 45) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw $Message
}
function Get-Status($Order) { (Invoke-RestMethod "$api/orders/$($Order.id)").status }
function Get-Queue($Name) {
    Invoke-RestMethod "$management/queues/%2F/$Name" -Headers $headers
}
function Publish($Payload) {
    $body = @{
        properties = @{ delivery_mode = 2; content_type = 'application/json' }
        routing_key = 'kitchen.orders'
        payload = $Payload
        payload_encoding = 'string'
    } | ConvertTo-Json -Compress
    $result = Invoke-RestMethod "$management/exchanges/%2F/amq.default/publish" -Headers $headers `
        -Method Post -ContentType application/json -Body $body
    Check $result.routed 'RabbitMQ did not route the test message'
}
function New-Order {
    $response = Invoke-WebRequest "$api/orders" -Method Post -ContentType application/json `
        -Body '{"customerId":1,"items":[{"menuItemId":1,"quantity":2}]}'
    $order = $response.Content | ConvertFrom-Json
    Check ($response.StatusCode -eq 201 -and $order.status -eq 'Pending') 'Expected 201 and Pending'
    Check ($response.Headers.Location[0] -eq "/orders/$($order.id)") 'Unexpected Location'
    Write-Host "POST 201 $($response.Headers.Location[0]) $($response.Content)"
    $order
}
function Wait-Api {
    Wait-Until {
        try { (Invoke-WebRequest "$api/menu" -TimeoutSec 2).StatusCode -eq 200 } catch { $false }
    } 'Order API is unavailable'
}

Push-Location (Join-Path $PSScriptRoot '..')
try {
    docker info --format '{{.ServerVersion}}'
    Compose up -d --wait postgres rabbitmq
    dotnet restore --configfile NuGet.Config
    dotnet tool install dotnet-ef --version 10.0.4 --tool-path ".local/$Project/ef" --configfile NuGet.Config
    & ".local/$Project/ef/dotnet-ef" database update --project src/Order.Api
    Compose up -d --build order-api
    Wait-Api
    $first = New-Order
    $sql = 'SELECT "Status" FROM "Orders" WHERE "Id" = ' + "'$($first.id)'"
    $status = $sql | docker compose -p $Project exec -T postgres psql -U kitchenflow -d kitchenflow -At
    Check ($status -eq 'Pending') 'PostgreSQL did not store Pending'
    $apiLogs = Compose logs --no-color order-api | Out-String
    Check ($apiLogs -match "RabbitMQ confirmed message .* for order $($first.id)") 'Confirmation missing'
    Write-Host 'PASS TC01–TC03 RabbitMQ connection, PostgreSQL Pending, confirm'

    Compose up -d --build kitchen-worker
    Wait-Until { (Get-Status $first) -eq 'Preparing' } 'Preparing was not observed'
    Wait-Until { (Get-Status $first) -eq 'Ready' } 'Ready was not observed'
    Write-Host 'PASS TC04–TC05 Preparing then Ready'

    $activeOrders = @(1..3 | ForEach-Object { New-Order })
    Wait-Until {
        $statuses = @($activeOrders | ForEach-Object { Get-Status $_ })
        @($statuses | Where-Object { $_ -ne 'Ready' }).Count -eq 0
    } 'Active orders did not finish'
    $workerLogs = Compose logs --no-color kitchen-worker | Out-String
    $lastReady = -1
    foreach ($order in $activeOrders) {
        $start = $workerLogs.IndexOf("Order $($order.id) is Preparing")
        $ready = $workerLogs.IndexOf("Order $($order.id) is Ready")
        Check ($start -gt $lastReady -and $ready -gt $start) 'Worker did not process sequentially'
        $lastReady = $ready
    }
    Write-Host 'PASS TC06 Three orders processed sequentially'

    Compose stop kitchen-worker
    $queued = @(1..3 | ForEach-Object { New-Order })
    Wait-Until { (Get-Queue 'kitchen.orders').messages_ready -eq 3 } 'Expected three queued messages'
    foreach ($order in $queued) { Check ((Get-Status $order) -eq 'Pending') 'Queued order is not Pending' }
    Write-Host 'PASS TC07 Three durable queued orders with Worker stopped'
    Compose start kitchen-worker
    foreach ($order in $queued) { Wait-Until { (Get-Status $order) -eq 'Ready' } 'Queued order not Ready' }
    Write-Host 'PASS TC08 Queued orders reached Ready after Worker restart'

    Publish (@{ messageId = [guid]::NewGuid(); orderId = $first.id; createdAt = [DateTime]::UtcNow } | ConvertTo-Json)
    Wait-Until {
        (Compose logs --no-color kitchen-worker | Out-String) -match "Skipping order $($first.id) in status Ready"
    } 'Duplicate was not skipped'
    Check ((Get-Status $first) -eq 'Ready') 'Duplicate changed the order state'
    Write-Host 'PASS TC09 Ready duplicate skipped'
    Publish '{'
    Wait-Until { (Get-Queue 'kitchen.orders.dead').messages_ready -ge 1 } 'Invalid message did not reach DLQ'
    Check ((Compose logs --no-color kitchen-worker | Out-String) -match 'Invalid kitchen message') 'Invalid message log missing'
    $afterPoison = New-Order
    Wait-Until { (Get-Status $afterPoison) -eq 'Ready' } 'Worker stopped after malformed message'
    Write-Host 'PASS TC10 Invalid message logged and dead-lettered; Worker still running'

    Compose restart order-api
    Wait-Api
    Check ((Get-Status $first) -eq 'Ready') 'Order did not persist after API restart'
    Write-Host 'PASS TC11 Order persisted after API restart'

    Compose stop rabbitmq
    $outage = New-Order
    $apiLogs = Compose logs --no-color order-api | Out-String
    Check ($apiLogs -match "Order $($outage.id) is saved but delivery .* could not be verified") 'Publish failure log missing'
    Check ($apiLogs -notmatch "confirmed message .* for order $($outage.id)") 'Unexpected confirmation'
    Check ((Get-Status $outage) -eq 'Pending') 'Outage order was not persisted'
    Compose up -d --wait rabbitmq
    $recovered = New-Order
    Wait-Until { (Get-Status $recovered) -eq 'Ready' } 'RabbitMQ processing did not recover'
    Write-Host 'PASS TC12 Unconfirmed publish logged; subsequent order recovered'

    Compose stop kitchen-worker
    $scratch = New-Item -ItemType Directory -Path ".local/$Project/regression/docs" -Force
    Push-Location $scratch.Parent.FullName
    try {
        & (Join-Path $PSScriptRoot 'Test-OrderApi.ps1') -BaseUrl $api
    } finally { Pop-Location }
    Write-Host 'PASS TC13 Week 1 HTTP regression suite'
} finally {
    Pop-Location
    Write-Host "Compose project retained for inspection: $Project"
}
