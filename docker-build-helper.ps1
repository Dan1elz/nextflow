# Script de ajuda para resolver problemas de build do Docker
# Uso: .\docker-build-helper.ps1 [clean|pull|build|all]

param(
    [Parameter(Position=0)]
    [ValidateSet("clean", "pull", "build", "all")]
    [string]$Action = "all"
)

function Clean-DockerCache {
    Write-Host "🧹 Limpando cache do Docker..." -ForegroundColor Yellow
    docker system prune -a --volumes -f
    Write-Host "✅ Cache limpo!" -ForegroundColor Green
}

function Pull-BaseImage {
    Write-Host "📥 Baixando imagem base do .NET SDK 8.0..." -ForegroundColor Yellow
    $maxRetries = 3
    $retryCount = 0
    
    while ($retryCount -lt $maxRetries) {
        try {
            docker pull mcr.microsoft.com/dotnet/sdk:8.0
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Imagem baixada com sucesso!" -ForegroundColor Green
                return
            }
        } catch {
            Write-Host "❌ Tentativa $($retryCount + 1) falhou" -ForegroundColor Red
        }
        
        $retryCount++
        if ($retryCount -lt $maxRetries) {
            $waitTime = [math]::Pow(2, $retryCount) * 5
            Write-Host "⏳ Aguardando $waitTime segundos antes de tentar novamente..." -ForegroundColor Yellow
            Start-Sleep -Seconds $waitTime
        }
    }
    
    Write-Host "❌ Falha ao baixar imagem após $maxRetries tentativas" -ForegroundColor Red
    exit 1
}

function Build-DockerCompose {
    Write-Host "🔨 Fazendo build do docker-compose..." -ForegroundColor Yellow
    docker-compose -f docker-compose.development.yml build --no-cache backend
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Build concluído com sucesso!" -ForegroundColor Green
    } else {
        Write-Host "❌ Build falhou!" -ForegroundColor Red
        exit 1
    }
}

switch ($Action) {
    "clean" {
        Clean-DockerCache
    }
    "pull" {
        Pull-BaseImage
    }
    "build" {
        Build-DockerCompose
    }
    "all" {
        Clean-DockerCache
        Pull-BaseImage
        Build-DockerCompose
    }
}

Write-Host "`n💡 Dicas:" -ForegroundColor Cyan
Write-Host "  - Se o problema persistir, verifique sua conexão de internet" -ForegroundColor Gray
Write-Host "  - Tente usar uma VPN se estiver atrás de um proxy restritivo" -ForegroundColor Gray
Write-Host "  - Verifique se o Docker Desktop está rodando corretamente" -ForegroundColor Gray

