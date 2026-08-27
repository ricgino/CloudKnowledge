FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/CloudKnowledge.Worker/CloudKnowledge.Worker.csproj
RUN dotnet publish src/CloudKnowledge.Worker/CloudKnowledge.Worker.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        poppler-utils \
        tesseract-ocr \
        tesseract-ocr-eng \
        tesseract-ocr-ita \
    && tesseract --version \
    && pdftoppm -v \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "CloudKnowledge.Worker.dll"]
