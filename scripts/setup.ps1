# Execution Policy (sessão atual)
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force

# -----------------------------------------
# UTF-8 para toda a sessão PowerShell
$PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()

Write-Host "[setup.ps1] UTF-8 ativo para input/output."


# -----------------------------------------
# 3º BLOCO — Criar repositório Git na pasta atual (repos/)
Write-Host "[setup.ps1] Detectar base dir..."

# Diretório atual (repos/)
$BaseDir = (Get-Location).Path
Write-Host "[setup.ps1] Base dir: $BaseDir"

# Perguntar nome do repositório
$RepoName = Read-Host "Nome do repositório a criar"

# Caminho final
$RepoPath = Join-Path $BaseDir $RepoName

# Criar pasta se não existir
if (!(Test-Path $RepoPath)) {
    New-Item -ItemType Directory -Path $RepoPath | Out-Null
    Write-Host "[setup.ps1] Pasta '$RepoName' criada."
} else {
    Write-Host "[setup.ps1] Pasta '$RepoName' já existe."
}

# Entrar na pasta
Set-Location $RepoPath

# Inicializar Git
git init
Write-Host "[setup.ps1] git init concluído em $RepoPath"


# -----------------------------------------
# 4º BLOCO — Criar ficheiros base do repositório
Write-Host "[setup.ps1] Criar ficheiros base..."

# Criar .gitignore (vazio)
New-Item -ItemType File ".gitignore" -Force | Out-Null

# Criar .gitattributes
Set-Content -Path ".gitattributes" -Value "* text=auto"

# Criar README.md
Set-Content -Path "README.md" -Value "# $RepoName`nProjeto criado automaticamente pelo setup.ps1."

# Criar CHANGELOG.md
Set-Content -Path "CHANGELOG.md" -Value "# Changelog`n`n## 0.1.0 — $(Get-Date -Format 'yyyy-MM-dd')`n- Estrutura inicial criada."

Write-Host "[setup.ps1] Ficheiros base criados."


# -----------------------------------------
# Commit inicial
$DoCommit = Read-Host "Queres fazer o primeiro commit? (s/n)"

if ($DoCommit -eq "s") {
    Write-Host "[setup.ps1] A preparar commit..."
    git add .
    git commit -m "Initial commit"
    Write-Host "[setup.ps1] Commit criado."
} else {
    Write-Host "[setup.ps1] Commit ignorado."
}
