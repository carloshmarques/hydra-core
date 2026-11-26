import sys
import re
import json
import sqlite3
from pathlib import Path
from datetime import datetime

def slugify(text: str) -> str:
    text = text.lower().strip()
    text = re.sub(r"[^\w\s-]", "", text)
    return re.sub(r"[\s_-]+", "-", text).strip("-")

def render_markdown(data: dict) -> str:
    stardate = data.get("stardate", datetime.now().strftime("%Y-%m-%d"))
    title = data.get("title") or data.get("subject", f"Captain's Log {stardate}")
    category = data.get("category") or data.get("domain", "SISTEMA")
    author = data.get("author", "Carlos Marques")
    tags = data.get("tags", ["logs", "on board diary"])
    tags_yaml = "[" + ", ".join(f'"{t}"' for t in tags) + "]"

    lines = [
        "---",
        "layout: posts",
        f'title: "{title}"',
        f"category: {category}",
        f"author: {author}",
        f"tags: {tags_yaml}",
        "---",
        "",
        f"# {title}",
        ""
    ]

    # Se vier no formato Keep a Changelog (Added/Changed/Fixed)
    if "changelog" in data and isinstance(data["changelog"], dict):
        for section, items in data["changelog"].items():
            if items:
                lines.append(f"## {section}")
                for item in items:
                    lines.append(f"- {item}")
                lines.append("")
    # Ou se vier com corpo livre Markdown
    elif data.get("body_markdown"):
        lines.append(data["body_markdown"].strip())
        lines.append("")

    # Renderiza as adendas cronológicas (Captain's Log, Supplemental)
    sup_logs = data.get("supplemental_logs", [])
    if sup_logs:
        lines.append("---")
        for sup in sup_logs:
            ts = sup.get("time") or sup.get("timestamp", "")
            note = sup.get("note", "").strip()
            if note:
                lines.append(f"### Supplemental [{ts}]")
                lines.append(note)
                lines.append("")

    return "\n".join(lines).strip() + "\n"

def convert_json_to_md_and_db(json_file: Path, root_dir: Path):
    data = json.loads(json_file.read_text(encoding="utf-8"))
    stardate = data.get("stardate", json_file.stem)
    title = data.get("title") or data.get("subject", "captains-log")

    md_content = render_markdown(data)

    # 1. Gera o ficheiro Markdown em _posts/YYYY-MM-DD-slug.md
    posts_dir = root_dir / "_posts"
    posts_dir.mkdir(parents=True, exist_ok=True)
    md_out = posts_dir / f"{stardate}-{slugify(title)}.md"
    md_out.write_text(md_content, encoding="utf-8")
    print(f"[OK] Markdown gerado a partir do JSON: {md_out}")

    # 2. Insere o Markdown renderizado na tabela Entradas + SupplementaryLogs
    db_path = root_dir / "data" / "sistema.sqlite"
    if db_path.exists():
        conn = sqlite3.connect(db_path)
        conn.execute("PRAGMA foreign_keys = ON;")
        cur = conn.cursor()

        cur.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='Entradas';")
        if cur.fetchone():
            cur.execute("SELECT IndexID FROM Indices LIMIT 1;")
            row = cur.fetchone()
            index_id = row[0] if row else 1
            estado = data.get("status", "Concluído")

            cur.execute("""
                INSERT INTO Entradas (IndexID, OwnerID, Data, Assunto, Descricao, Estado, TipoMovimento)
                VALUES (?, 1, ?, ?, ?, ?, 'Nota')
            """, (index_id, f"{stardate} 00:00:00", title, md_content, estado))
            entrada_id = cur.lastrowid

            for sup in data.get("supplemental_logs", []):
                ts = sup.get("time") or sup.get("timestamp", "00:00:00")
                if len(ts) <= 8:
                    ts = f"{stardate} {ts}"
                cur.execute("""
                    INSERT INTO SupplementaryLogs (EntradaID, DataHora, NotaAdenda)
                    VALUES (?, ?, ?)
                """, (entrada_id, ts, sup.get("note", "")))

            conn.commit()
            print(f"[OK] Consolidado em SQLite (EntradaID={entrada_id})")
        conn.close()

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Uso: python scripts/json_to_md.py _docs/captains-log/2026-11-26.json")
        sys.exit(1)

    root = Path(__file__).resolve().parent.parent
    target_json = (root / sys.argv[1]).resolve()
    if not target_json.exists():
        print(f"[-] Ficheiro JSON não encontrado: {target_json}")
        sys.exit(1)

    convert_json_to_md_and_db(target_json, root)
