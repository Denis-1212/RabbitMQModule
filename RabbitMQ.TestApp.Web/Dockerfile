FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["RabbitMQ.TestApp.Web/RabbitMQ.TestApp.Web.csproj", "RabbitMQ.TestApp.Web/"]
COPY ["RabbitMQ.Module/RabbitMQ.Module.csproj", "RabbitMQ.Module/"]
RUN dotnet restore "RabbitMQ.TestApp.Web/RabbitMQ.TestApp.Web.csproj"

COPY . .
WORKDIR "/src/RabbitMQ.TestApp.Web"
RUN dotnet build "RabbitMQ.TestApp.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RabbitMQ.TestApp.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RabbitMQ.TestApp.Web.dll"]