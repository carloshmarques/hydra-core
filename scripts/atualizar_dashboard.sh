#!/bin/bash

# Arquivo de saída
OUTPUT="dashboard.md"

# Coleta de dados básicos do sistema (Linux)
DATA_ATUAL=$(date +"%d/%m/%Y %H:%M:%S")
UPTIME=$(uptime -p)
MEM_LIVRE=$(free -m | awk '/Mem:/ {print $4"MB"}')

# Escreve a estrutura do Dashboard no arquivo .md
cat << EOF > $OUTPUT
# 🐚 Painel de Monitoramento do Servidor

Atualizado em: \`$DATA_ATUAL\`

---

### 📊 Status da Máquina
> **Tempo Ativo:** \`$UPTIME\` | **Memória Livre:** \`$MEM_LIVRE\`

---

### 🛠️ Processos Ativos (Top 5 CPU)
\`\`\`bash
$(ps -eo pid,ppid,cmd,%mem,%cpu --sort=-%cpu | head -n 6)
\`\`\`

---
*Gerado automaticamente via Script Shell (.sh).*
EOF

echo "Dashboard .md gerado com sucesso!"
