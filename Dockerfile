FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY NuGet.Config ./
COPY src ./src
RUN dotnet publish src/KitchenFlow.KitchenWorker -c Release -o /out/worker
RUN dotnet publish src/Order.Api -c Release -o /out/api

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
USER app
ENTRYPOINT ["dotnet", "Order.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
WORKDIR /app
COPY --from=build /out/worker .
USER app
ENTRYPOINT ["dotnet", "KitchenFlow.KitchenWorker.dll"]
