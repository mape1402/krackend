FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT_PATH
WORKDIR /source
COPY . .
RUN dotnet publish "$PROJECT_PATH" -c Release -f net10.0 -o /out -m:1 -p:MinVerSkip=true -p:BuildInParallel=false
COPY agents/stress/docker/appsettings.json /out/appsettings.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG APP_DLL
ENV APP_DLL=$APP_DLL
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Demo
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet \"$APP_DLL\""]

