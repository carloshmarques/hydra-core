import sys
import re
import json
import sqlite3
from pathlib import Path
from datetime import datetime

def parse_frontmatter_and_body(md_text: str):
    match = re.match(r"^---\s*\n(.*?)\n---\s*\n(.*)$", md_text, re.DOTALL)
    if not match:
        return {}, md_text.strip()

    raw_yaml, body = match.group(1), match.group(2).strip()
    meta = {}
    for line in raw_yaml.splitlines():
        if ":" not in line:
            continue
        k, v = line.split(":", 1)
        k = k.strip()
        v = v.strip().strip('"').strip("'")
        if v.startswith("[") and v.endswith("]"):
            items = [i.strip().strip('"').strip("'") for i in v[1:-1].split(",") if i.strip()]
            meta[k] = items
        else:
            meta[k] = v
    return meta, body

def extract_supplemental_logs(body: str):
    """Procura secções '### Supplemental [HH:MM]' ou '> Supplemental:' no corpo Markdown."""
    logs = []
    pattern = re.findall(r"(?:###\s*Supplemental\s*\[?([\d:]+)\]?|>+\s*Supplemental\s*\[?([\d:]+)\]?:?)\s*\n?(.*?)(?=\n###|\Z)", body, re.DOTALL | re.IGNORECASE)
    for t1, t2, note in pattern:
        ts = t1 or t2 or datetime.now().strftime("%H:%M:%S")
        clean_note = note.strip()
        if clean_note:
            logs.append({"time": ts, "note": clean_note})
    return logs

def convert_md_to_json_and_db(md_file: Path, root_dir: Path):
    md_text = md_file.read_text(encoding="utf-8")
    meta, body = parse_frontmatter_and_body(md_text)

    date_match = re.match(r"^(\d{4}-\d{2}-\d{2})", md_file.name)
    stardate = date_match.group(1) if date_match else datetime.now().strftime("%Y-%m-%d")

    payload = {
        "stardate": stardate,
        "title": meta.get("title", md_file.stem),
        "category": meta.get("category", "SISTEMA"),
        "author": meta.get("author", "Carlos Marques"),
        "tags": meta.get("tags", []),
        "body_markdown": body,
        "supplemental_logs": extract_supplemental_logs(body),
        "synced_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    }

    # 1. Guarda JSON limpo em _docs/captain's-log/YYYY-MM-DD.json
    log_dir = root_dir / "_docs" / "captain's-log"
    log_dir.mkdir(parents=True, exist_ok=True)
    json_out = log_dir / f"{stardate}.json"
    json_out.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"[OK] JSON gerado: {json_out}")

    # 2. Insere na tabela Entradas + SupplementaryLogs se a BD existir
    db_path = root_dir / "data" / "sistema.sqlite"
    if db_path.exists():
        conn = sqlite3.connect(db_path)
        conn.execute("PRAGMA foreign_keys = ON;")
        cur = conn.cursor()

        # Garante um IndexID padrão para Captain's Log se a tabela existir
        cur.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='Entradas';")
        if cur.fetchone():
            cur.execute("SELECT IndexID FROM Indices LIMIT 1;")
            row = cur.fetchone()
            index_id = row[0] if row else 1

            cur.execute("""
                INSERT INTO Entradas (IndexID, OwnerID, Data, Assunto, Descricao, Estado, TipoMovimento)
                VALUES (?, 1, ?, ?, ?, 'Concluído', 'Nota')
            """, (index_id, f"{stardate} 00:00:00", payload["title"], body))
            entrada_id = cur.lastrowid

            for sup in payload["supplemental_logs"]:
                cur.execute("""
                    INSERT INTO SupplementaryLogs (EntradaID, DataHora, NotaAdenda)
                    VALUES (?, ?, ?)
                """, (entrada_id, f"{stardate} {sup['time']}", sup["note"]))

            conn.commit()
            print(f"[OK] Inserido em SQLite (EntradaID={entrada_id}, Adendas={len(payload['supplemental_logs'])})")
        conn.close()

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Uso: python scripts/md_to_json.py _posts/2026-11-26-captains-log.md")
        sys.exit(1)

    root = Path(__file__).resolve().parent.parent
    target_md = (root / sys.argv[1]).resolve()
    if not target_md.exists():
        print(f"[-] Ficheiro não encontrado: {target_md}")
        sys.exit(1)

    convert_md_to_json_and_db(target_md, root)
