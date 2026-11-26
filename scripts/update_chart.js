const fs = require('fs');

async function updateReadme() {
  try {
    // 1. Lê o ficheiro JSON local em vez da API do GitHub
    const rawData = fs.readFileSync('./data.json', 'utf8');
    const dados = JSON.parse(rawData);
    
    // 2. Lógica Pivot: Agrupa as horas (ou quantidade) por tecnologia
    const pivotData = {};
    dados.forEach(item => {
      if (item.tecnologia) {
        // Soma as horas ou conta a ocorrência
        pivotData[item.tecnologia] = (pivotData[item.tecnologia] || 0) + (item.horas || 1);
      }
    });

    // 3. Configuração do Mini-Banner Compacto dentro de uma DIV
    let mermaidChart = '<div align="center" style="width: 100%; max-width: 400px; margin: auto;">\n\n';
    mermaidChart += '```mermaid\n';
    mermaidChart += '%%{init: {\n';
    mermaidChart += '  "theme": "dark",\n';
    mermaidChart += '  "themeVariables": {\n';
    mermaidChart += '    "pieFontSize": "11px",\n';
    mermaidChart += '    "pieLegendTextSize": "11px"\n';
    mermaidChart += '  },\n';
    mermaidChart += '  "pie": { "textPosition": 0.5 }\n';
    mermaidChart += '}}%%\n';
    mermaidChart += 'pie title ""\n'; // Sem título gigante para parecer um banner
    
    for (const [tech, valor] of Object.entries(pivotData)) {
      mermaidChart += `    "${tech}" : ${valor}\n`;
    }
    mermaidChart += '```\n\n';
    mermaidChart += '</div>';

    // 4. Injeta no README.md
    const readmePath = './README.md';
    let readmeContent = fs.readFileSync(readmePath, 'utf8');
    
    const startMarker = '<!-- PIVOT_CHART_START -->';
    const endMarker = '<!-- PIVOT_CHART_END -->';
    
    const pattern = new RegExp(`${startMarker}[\\s\\S]*?${endMarker}`);
    const updatedContent = readmeContent.replace(pattern, `${startMarker}\n${mermaidChart}\n${endMarker}`);
    
    fs.writeFileSync(readmePath, updatedContent);
    console.log('Banner minimalista gerado a partir do JSON local!');
    
  } catch (error) {
    console.error('Erro ao processar ficheiro local:', error);
  }
}

updateReadme();
