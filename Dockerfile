FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PfnTimeTracking.csproj", "./"]
RUN dotnet restore "PfnTimeTracking.csproj"
COPY . .
RUN dotnet publish "PfnTimeTracking.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/Data
ENTRYPOINT ["dotnet", "PfnTimeTracking.dll"]
