"""Local feasibility probe, NOT an API exporter or a general-purpose PDF editor.

Preserves original content operations and fonts. Changes one single-line category
only when the embedded font already contains every required glyph and it fits.
Does not use a network, a model, a database, OCR, white rectangles or new fonts.
"""
from __future__ import annotations

import argparse
import io
import json
import re
import unicodedata
from pathlib import Path

import pdfplumber
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ByteStringObject, ContentStream
from review_required import ReviewRequired
from skill_insertion_plan_model import SkillInsertionPlanModel


def _normalize(value: str) -> str:
    return unicodedata.normalize("NFKC", value).strip().casefold()


def _font_map(font) -> dict[int, str]:
    if font.get("/Subtype") != "/TrueType" or "/ToUnicode" not in font:
        raise ReviewRequired("A fonte precisa de mapeamento Unicode TrueType simples.")
    cmap = font["/ToUnicode"].get_data().decode("ascii", errors="strict")
    if "beginbfrange" in cmap:
        raise ReviewRequired("Mapeamento de fonte em intervalos ainda não suportado.")
    mapping = {}
    for block in re.findall(r"beginbfchar(.*?)endbfchar", cmap, re.S):
        for encoded, unicode_hex in re.findall(r"<([\da-fA-F]+)>\s*<([\da-fA-F]+)>", block):
            if len(encoded) != 2:
                raise ReviewRequired("Fonte multibyte ainda não suportada.")
            mapping[int(encoded, 16)] = bytes.fromhex(unicode_hex).decode("utf-16-be")
    if not mapping:
        raise ReviewRequired("Mapeamento Unicode indisponível.")
    return mapping


def _raw(value) -> bytes:
    if isinstance(value, bytes):
        return bytes(value)
    return value.original_bytes


def _rows(page):
    rows = {}
    for char in page.chars:
        a, b, c, d, _, baseline = char["matrix"]
        if not char.get("upright", False) or abs(b) > .001 or abs(c) > .001 or abs(a - 1) > .001 or abs(d - 1) > .001:
            raise ReviewRequired("Texto transformado/rotacionado ainda não suportado.")
        rows.setdefault(round(baseline, 2), []).append(char)
    return [sorted(chars, key=lambda x: x["x0"]) for _, chars in sorted(rows.items(), reverse=True)]


def plan_skill_insertion(source: bytes, section: str, category: str, skill: str) -> SkillInsertionPlanModel:
    if not section.strip() or not category.strip() or not skill.strip():
        raise ReviewRequired("Seção, categoria e habilidade são obrigatórias.")
    if any(c in skill for c in "\n\r,;:") or not any(c.isalnum() for c in skill):
        raise ReviewRequired("Informe uma única habilidade, não um parágrafo ou lista.")
    reader = PdfReader(io.BytesIO(source))
    if reader.is_encrypted or len(reader.pages) != 1:
        raise ReviewRequired("A prova aceita apenas PDF digital de uma página sem criptografia.")
    if "/AcroForm" in reader.trailer["/Root"]:
        raise ReviewRequired("Formulários/assinaturas não são suportados nesta prova.")
    page = reader.pages[0]
    if page.rotation or tuple(page.cropbox) != tuple(page.mediabox) or tuple(page.mediabox.lower_left) != (0, 0):
        raise ReviewRequired("Rotação ou recorte de página não suportado.")
    if page.get("/Resources", {}).get("/XObject"):
        raise ReviewRequired("Imagens e XObjects precisam de validação específica.")
    annotations = [a.get_object() for a in page.get("/Annots", [])]
    for annotation in annotations:
        action = annotation.get("/A", {}).get_object() if annotation.get("/A") else {}
        if annotation.get("/Subtype") != "/Link" or action.get("/S") != "/URI" or "/AA" in annotation:
            raise ReviewRequired("Somente links fora do trecho alterado são suportados.")

    with pdfplumber.open(io.BytesIO(source)) as document:
        visual_page = document.pages[0]
        if not visual_page.chars:
            raise ReviewRequired("PDF sem texto digital; OCR não faz parte da prova.")
        rows = _rows(visual_page)
        texts = ["".join(c["text"] for c in row).strip() for row in rows]
        sections = [i for i, text in enumerate(texts) if _normalize(text) == _normalize(section)]
        if len(sections) != 1:
            raise ReviewRequired("Seção ausente ou ambígua.")
        start = sections[0] + 1
        # Narrow scope: uppercase heading on its own row separates sections.
        end = next((i for i in range(start, len(rows)) if texts[i].isupper() and ":" not in texts[i]), len(rows))
        prefix = category.strip() + ":"
        matches = [i for i in range(start, end) if _normalize(texts[i]).startswith(_normalize(prefix))]
        if len(matches) != 1:
            raise ReviewRequired("Categoria ausente ou ambígua dentro da seção.")
        row_index = matches[0]
        if row_index + 1 < end and ":" not in texts[row_index + 1]:
            raise ReviewRequired("Categoria com continuação em outra linha exige reflow.")
        row = rows[row_index]
        for annotation in annotations:
            rect = annotation.get("/Rect")
            if not rect or (float(rect[1]) < max(c["y1"] for c in row) and float(rect[3]) > min(c["y0"] for c in row)):
                raise ReviewRequired("Link sobre o trecho alterado exige revisão.")
        joined = "".join(c["text"] for c in row)
        colon = joined.index(":")
        body = joined[colon + 1:].strip()
        if not body:
            raise ReviewRequired("Categoria vazia.")
        if _normalize(skill).rstrip(".") in {_normalize(x).rstrip(".") for x in re.split(r"[,;]", body)}:
            raise ReviewRequired("Habilidade já presente; nenhuma alteração necessária.")
        # Each supported glyph must represent a single Unicode character.
        if any(len(c["text"]) != 1 for c in row):
            raise ReviewRequired("Ligaturas precisam de validação específica.")
        body_chars = row[colon + 1:]
        while body_chars and body_chars[0]["text"].isspace():
            body_chars = body_chars[1:]
        while body_chars and body_chars[-1]["text"].isspace():
            body_chars = body_chars[:-1]
        if len({(c["fontname"], round(c["size"], 3), str(c.get("non_stroking_color"))) for c in body_chars}) != 1:
            raise ReviewRequired("Corpo da categoria com estilos mistos não suportado.")
        left = min(c["x0"] for c in visual_page.chars)
        right = float(visual_page.width) - left
        x = body_chars[0]["x0"]
        size = body_chars[0]["size"]
        original_width = body_chars[-1]["x1"] - x
        available_width = right - x
        width, height = float(visual_page.width), float(visual_page.height)
        visual_font = body_chars[0]["fontname"]

    stream = ContentStream(page.get_contents(), reader)
    active_font = None
    word_spacing = char_spacing = 0.0
    horizontal_scale = 100.0
    state_stack = []
    candidates = []
    for index, (operands, operator) in enumerate(stream.operations):
        if operator in (b"BDC", b"BMC", b"EMC"):
            raise ReviewRequired("Conteúdo marcado/ActualText precisa de validação específica.")
        if operator in (b"TJ", b"'", b'"'):
            raise ReviewRequired("Operações de texto posicionadas exigem outro adaptador.")
        if operator == b"q":
            state_stack.append((active_font, word_spacing, char_spacing, horizontal_scale))
        if operator == b"Q" and state_stack:
            active_font, word_spacing, char_spacing, horizontal_scale = state_stack.pop()
        if operator == b"Tw":
            word_spacing = float(operands[0])
        if operator == b"Tc":
            char_spacing = float(operands[0])
        if operator == b"Tz":
            horizontal_scale = float(operands[0])
        if operator == b"Tf":
            active_font = operands[0]
        if operator != b"Tj" or active_font is None:
            continue
        font = page["/Resources"]["/Font"][active_font].get_object()
        if str(font.get("/BaseFont", "")).lstrip("/") != visual_font:
            continue
        mapping = _font_map(font)
        raw = _raw(operands[0])
        decoded = "".join(mapping.get(c, "\ufffd") for c in raw)
        if decoded.strip() != body:
            continue
        if word_spacing or char_spacing or horizontal_scale != 100:
            raise ReviewRequired("Trecho com espaçamento/escala customizados exige revisão.")
        # Do not remove text that another Tj relies on for relative placement.
        if index + 1 >= len(stream.operations) or stream.operations[index + 1][1] not in (b"T*", b"ET"):
            raise ReviewRequired("Operações seguintes dependem da posição do texto alterado.")
        reverse = {v: k for k, v in mapping.items() if len(v) == 1}
        separator = "; " if ";" in body and "," not in body else ", "
        adapted = body.rstrip(".").rstrip() + separator + skill.strip().rstrip(".") + ("." if body.endswith(".") else "")
        # Preserve leading/trailing spaces used by the original text operation.
        adapted = decoded[:len(decoded) - len(decoded.lstrip())] + adapted + decoded[len(decoded.rstrip()):]
        if any(c not in reverse for c in adapted):
            raise ReviewRequired("A fonte original não contém todos os caracteres; não será substituída.")
        encoded = bytes(reverse[c] for c in adapted)
        widths = font.get("/Widths")
        first = int(font.get("/FirstChar", 0))
        if not widths or any(c < first or c - first >= len(widths) for c in raw + encoded):
            raise ReviewRequired("Métricas da fonte indisponíveis.")
        measure = lambda data: sum(float(widths[c - first]) for c in data) * size / 1000
        # Check metrics against visible glyphs before trusting the fit calculation.
        trimmed = bytes(reverse[c] for c in body)
        if abs(measure(trimmed) - original_width) > .5:
            raise ReviewRequired("Métricas divergentes do PDF original.")
        adapted_width = measure(bytes(reverse[c] for c in adapted.strip()))
        if adapted_width > available_width:
            raise ReviewRequired("Não cabe na mesma linha. Requer revisão; fonte e paginação não serão alteradas.")
        candidates.append(SkillInsertionPlanModel(index, encoded, width, height, original_width, adapted_width, available_width))
    if len(candidates) != 1:
        raise ReviewRequired("Trecho não localizado de forma única no fluxo de conteúdo.")
    return candidates[0]


def apply_skill_insertion(source: bytes, section: str, category: str, skill: str, *, confirmed: bool) -> bytes:
    if not confirmed:
        raise ReviewRequired("Inclusão factual depende de confirmação explícita.")
    plan = plan_skill_insertion(source, section, category, skill)
    reader = PdfReader(io.BytesIO(source))
    writer = PdfWriter()
    writer.clone_document_from_reader(reader)
    page = writer.pages[0]
    stream = ContentStream(page.get_contents(), writer)
    operands, operator = stream.operations[plan.operation_index]
    stream.operations[plan.operation_index] = ([ByteStringObject(plan.encoded_text)], operator)
    page.replace_contents(stream)
    output = io.BytesIO()
    writer.write(output)
    return output.getvalue()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--section", required=True)
    parser.add_argument("--category", required=True)
    parser.add_argument("--skill", required=True)
    parser.add_argument("--confirmed", action="store_true")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        source = args.source.read_bytes()
        plan = plan_skill_insertion(source, args.section, args.category, args.skill)
        if args.output:
            if args.output.resolve() == args.source.resolve():
                raise ReviewRequired("O arquivo original nunca pode ser sobrescrito.")
            result = apply_skill_insertion(source, args.section, args.category, args.skill, confirmed=args.confirmed)
            with args.output.open("xb") as output:
                output.write(result)
        print(json.dumps({"status": "generated" if args.output else "fits_without_reflow", "pages": 1,
                          "pageSize": [plan.page_width, plan.page_height],
                          "remainingWidthPoints": round(plan.available_width - plan.adapted_width, 2)}, ensure_ascii=False))
        return 0
    except ReviewRequired as error:
        print(json.dumps({"status": "review_required", "reason": str(error)}, ensure_ascii=False))
        return 2
    except (OSError, ValueError, KeyError, TypeError, UnicodeError):
        print(json.dumps({"status": "review_required", "reason": "Arquivo indisponível, saída existente ou estrutura não suportada."}, ensure_ascii=False))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
