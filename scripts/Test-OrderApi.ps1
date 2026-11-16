param([string]$BaseUrl = 'http://localhost:5080', [string]$SavedLocation = '')
$ErrorActionPreference = 'Stop'
function Check($condition, $message) { if (!$condition) { throw $message } }
function Call($method, $path, $body = $null, $type = 'application/json') {
    $args = @{ Method=$method; Uri="$BaseUrl$path"; SkipHttpErrorCheck=$true }
    if ($null -ne $body) { $args.Body=$body; $args.ContentType=$type }
    Invoke-WebRequest @args
}
if ($SavedLocation) {
    $saved = Get-Content docs/created-order.json -Raw | ConvertFrom-Json
    $r = Call GET $SavedLocation
    Check ($r.StatusCode -eq 200) 'Restart lookup failed'
    Check ($r.Content -eq $saved.body) 'Order changed after restart'
    Write-Output "PASS restart $SavedLocation HTTP 200; identical body"
    exit
}
$r = Call GET /menu
Check ($r.StatusCode -eq 200 -and @($r.Content | ConvertFrom-Json).Count -ge 3) 'Menu failed'
Write-Output 'PASS menu 200'
$before = @((Call GET /orders).Content | ConvertFrom-Json).Count
$r = Call POST /orders '{"customerId":1,"items":[{"menuItemId":1,"quantity":2},{"menuItemId":2,"quantity":1}]}'
Check ($r.StatusCode -eq 201) "Create expected 201, got $($r.StatusCode)"
$location = [string]$r.Headers.Location[0]
$o = $r.Content | ConvertFrom-Json
Check ($o.status -ceq 'Pending') 'Status failed'
Check ($location -eq "/orders/$($o.id)") 'Location failed'
@{ location=$location; body=$r.Content } | ConvertTo-Json | Set-Content docs/created-order.json
$r.RawContent | Set-Content docs/create-response.txt
Write-Output "PASS create 201 Location=$location Pending"
$get = Call GET $location
Check ($get.StatusCode -eq 200 -and $get.Content -eq $r.Content) 'Detail mismatch'
Check ($o.items.Count -eq 2 -and $o.items[0].quantity -eq 2 -and $o.items[1].quantity -eq 1) 'Quantities mismatch'
Write-Output 'PASS detail 200 quantities 2,1'
$list = Call GET /orders
$orders = @($list.Content | ConvertFrom-Json)
Check ($list.StatusCode -eq 200 -and $orders[0].id -eq $o.id) 'List failed'
Write-Output 'PASS list 200 newest first'
$r = Call GET /orders/00000000-0000-0000-0000-000000000001
Check ($r.StatusCode -eq 404) 'Not found failed'
Write-Output 'PASS absent order 404'
$bad = @(
    '{"customerId":1,"items":[]}',
    '{"customerId":999999,"items":[{"menuItemId":1,"quantity":1}]}',
    '{"customerId":1,"items":[{"menuItemId":999999,"quantity":1}]}',
    '{"customerId":1,"items":[{"menuItemId":1,"quantity":0}]}',
    '{"customerId":1,"items":[{"menuItemId":1,"quantity":1},{"menuItemId":1,"quantity":2}]}',
    '{"customerId":1,"items":null}',
    '{"customerId":1,"items":[null]}'
)
foreach ($body in $bad) {
    $r = Call POST /orders $body
    $errorDto = $r.Content | ConvertFrom-Json
    Check ($r.StatusCode -eq 400 -and $errorDto.code -and $errorDto.message) "Validation failed: $body"
    Write-Output "PASS validation 400 $($errorDto.code)"
}
foreach ($case in @(@('POST','/orders','{','application/json'),@('POST','/orders','{}','text/plain'),@('GET','/orders/not-a-guid',$null,'application/json'))) {
    $r = Call $case[0] $case[1] $case[2] $case[3]
    $expected = if ($case[3] -eq 'text/plain') {415} else {400}
    Check ($r.StatusCode -eq $expected) 'Framework rejection failed'
    Write-Output "Framework $($case[1]) => $($r.StatusCode); content-type=$($r.Headers['Content-Type']); body-length=$($r.Content.Length)"
}
$after = @((Call GET /orders).Content | ConvertFrom-Json).Count
Check ($after -eq $before + 1) 'Rejected requests created orders'
Write-Output 'PASS rejected requests did not create orders'
