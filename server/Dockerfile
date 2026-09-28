FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY . .
RUN dotnet publish server/VectorSpace.Server/VectorSpace.Server.csproj -c Release -o /out --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir -p /data/vectorspace && chown app:app /data/vectorspace
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:8080
ENV VECTORSPACE_DATA=/data/vectorspace
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "VectorSpace.Server.dll"]
