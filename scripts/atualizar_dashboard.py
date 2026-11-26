import os
from datetime import datetime

def generate_dashboard():
    # 1. Simular coleta de dados ou logs
    data_atual = datetime.now().strftime("%d/%m/%Y %H:%M")
    
    # Dados fictícios de exemplo (Pode vir de um banco, CSV ou API)
    kpis = {
        "status": "Ativo 🟢",
        "requisicoes": 14205,
        "erros": 12,
        "cpu": "24%",
        "memoria": "58%"
    }
    
    # 2. Montar o conteúdo em Markdown
    md_content = f"""# 📊 Painel de Monitoramento Automático

Atualizado em: `{data_atual}`

---

### 🖥️ Recursos do Sistema
> **Status:** {kpis['status']} | **CPU:** `{kpis['cpu']}` | **Memória:** `{kpis['memoria']}`

---

### 📈 Métricas de Tráfego

| Métrica | Valor Atual | Status |
| :--- | :---: | :---: |
| **Total de Requisições** | {kpis['requisicoes']:,} | Estável |
| **Taxa de Erros (5xx)** | {kpis['erros']} | Baixa 🟢 |

---

### 📂 Logs Recentes (Últimas Atividades)
- [x] Backup do banco de dados concluído (`02:00`)
- [x] Limpeza de cache realizada (`04:00`)
- [ ] Verificação de certificados SSL (Pendente)

---
*Gerado automaticamente via Script Python.*
"""
    
    # 3. Salvar o arquivo final .md
    output_path = "dashboard.md"
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(md_content)
    
    print(f"Dashboard atualizado com sucesso em: {output_path}")

if __name__ == "__main__":
    generate_dashboard()
