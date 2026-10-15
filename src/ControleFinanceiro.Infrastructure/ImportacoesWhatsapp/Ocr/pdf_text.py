"""Extract embedded text locally. Password arrives through stdin and is never written."""
import json
import sys
from pathlib import Path
from pypdf import PdfReader


def read_pdf(source, password):
    try:
        reader = PdfReader(source)
        if reader.is_encrypted and (not password or not reader.decrypt(password)):
            return {"Pages": [], "Error": "O PDF está protegido. Informe a senha correta do arquivo."}
        if len(reader.pages) > 20:
            return {"Pages": [], "Error": "O PDF deve ter no máximo 20 páginas."}
        pages = []
        size = 0
        for page in reader.pages:
            text = page.extract_text(extraction_mode="layout", layout_mode_space_vertically=False) or ""
            size += len(text)
            if size > 1_000_000:
                return {"Pages": [], "Error": "O conteúdo do PDF excede o limite de leitura."}
            pages.append([line for line in text.splitlines() if line.strip()])
        return {"Pages": pages, "Error": None}
    except Exception:
        return {"Pages": [], "Error": "Não foi possível extrair o texto do PDF. Verifique o arquivo e sua senha."}


if __name__ == "__main__":
    password = json.loads(sys.stdin.read())
    result = read_pdf(sys.argv[1], password)
    Path(sys.argv[2]).write_text(json.dumps(result, ensure_ascii=False), encoding="utf-8")
