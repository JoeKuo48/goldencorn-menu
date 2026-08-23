FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY ["GoldenCornOrder.csproj", "./"]
RUN dotnet restore "GoldenCornOrder.csproj"
COPY . .
RUN dotnet publish "GoldenCornOrder.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableWriteXorExecute=0
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80
ENTRYPOINT ["dotnet", "GoldenCornOrder.dll"]
