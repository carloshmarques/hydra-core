import os
import sys

# Mapping of the most common file extensions
LANGUAGES = {
    '.py': 'Python',
    '.js': 'JavaScript',
    '.ts': 'TypeScript',
    '.html': 'HTML',
    '.css': 'CSS',
    '.md': 'Markdown',
    '.json': 'JSON',
    '.sh': 'Shell Script'
}

def count_lines_native(root_path):
    stats = {}
    ignored_dirs = {'.git', '__pycache__', 'node_modules', 'venv', '.venv'}

    for root, dirs, files in os.walk(root_path):
        dirs[:] = [d for d in dirs if d not in ignored_dirs]

        for file in files:
            ext = os.path.splitext(file)[1].lower()
            
            if ext in LANGUAGES:
                lang_name = LANGUAGES[ext]
                full_path = os.path.join(root, file)
                
                try:
                    with open(full_path, 'r', encoding='utf-8', errors='ignore') as f:
                        lines = sum(1 for _ in f)
                    
                    if lang_name not in stats:
                        stats[lang_name] = {'files': 0, 'lines': 0}
                    
                    stats[lang_name]['files'] += 1
                    stats[lang_name]['lines'] += lines
                except:
                    continue

    if not stats:
        return "<!-- No data found -->"
        
        # Painel Customizado Dark (GitHub‑style)
    html = '<div style="background-color: #0d1117; border: 1px solid #30363d; border-radius: 8px; padding: 16px; box-shadow: 0 4px 10px rgba(0,0,0,0.3); width: 100%; box-sizing: border-box; font-family: -apple-system, BlinkMacSystemFont, \'Segoe UI\', Helvetica, Arial, sans-serif;">\n'
    html += '  <table style="border-collapse: collapse; margin: 0; width: 100%; border: none;">\n'
    html += '    <thead>\n'
    html += '      <tr style="border-bottom: 2px solid #1f6feb; color: #58a6ff; font-size: 13px;">\n'
    html += '        <th style="text-align: left; padding: 6px 12px; padding-left: 5px;">Language</th>\n'
    html += '        <th style="text-align: center; padding: 6px 12px;">Files</th>\n'
    html += '        <th style="text-align: center; padding: 6px 12px; padding-right: 5px;">Lines</th>\n'
    html += '      </tr>\n'
    html += '    </thead>\n'
    html += '    <tbody style="color: #c9d1d9; font-size: 13px;">\n'
    
    for lang, data in sorted(stats.items(), key=lambda x: x[1]['lines'], reverse=True):
        html += '      <tr style="border-bottom: 1px solid #30363d;">\n'
        html += f'        <td style="text-align: left; padding: 8px 12px; padding-left: 5px; color: #3fb950;"><b>{lang}</b></td>\n'
        html += f'        <td style="text-align: center; padding: 8px 12px; color: #f2cc60;">{data["files"]}</td>\n'
        html += f'        <td style="text-align: center; padding: 8px 12px; padding-right: 5px; color: #58a6ff; font-weight: bold;">{data["lines"]}</td>\n'
        html += '      </tr>\n'
        
    html += '    </tbody>\n'
    html += '  </table>\n'
    html += '</div>\n'

    
    return html

def update_readme():
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))
    PROJ_ROOT = os.path.abspath(os.path.join(BASE_DIR, ".."))
    README_PATH = os.path.join(PROJ_ROOT, "README.md")

    if not os.path.exists(README_PATH):
        print(f"Error: README.md not found at {README_PATH}")
        sys.exit(1)

    html_output = count_lines_native(PROJ_ROOT)

    with open(README_PATH, "r", encoding="utf-8") as f:
        readme = f.read()

    start_tag, end_tag = "<!-- LANGUAGES_START -->", "<!-- LANGUAGES_END -->"
    start_idx = readme.find(start_tag)
    end_idx = readme.find(end_tag)

    if start_idx == -1 or end_idx == -1:
        print(f"Error: Could not find {start_tag} or {end_tag} in your README.md.")
        sys.exit(1)

    new_block = f"{start_tag}\n{html_output}{end_tag}"
    updated_readme = readme[:start_idx] + new_block + readme[end_idx + len(end_tag):]

    with open(README_PATH, "w", encoding="utf-8") as f:
        f.write(updated_readme)

    print("Dashboard successfully updated with perfect Dracula alignment!")

if __name__ == "__main__":
    update_readme()
