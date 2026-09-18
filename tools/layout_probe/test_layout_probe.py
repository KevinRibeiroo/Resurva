"""Synthetic fixtures only. Runs locally without Firebase, database or Gemini."""
import io
import tempfile
import unittest
import contextlib
import sys
from unittest.mock import patch
from pathlib import Path

import pdfplumber
import reportlab
from pypdf import PdfReader
from pypdf.generic import ContentStream
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen.canvas import Canvas

from layout_probe import ReviewRequired, apply_skill_insertion, plan_skill_insertion, main


def synthetic_resume(*, long_body=False, duplicate=False, multiline=False, two_pages=False, link_y=None):
    font_dir = Path(reportlab.__file__).parent / "fonts"
    pdfmetrics.registerFont(TTFont("ProbeBody", str(font_dir / "Vera.ttf")))
    pdfmetrics.registerFont(TTFont("ProbeBold", str(font_dir / "VeraBd.ttf")))
    output = io.BytesIO()
    canvas = Canvas(output, pagesize=(612, 792), invariant=1)
    canvas.setFont("ProbeBold", 15)
    canvas.drawCentredString(306, 755, "CANDIDATO EXEMPLO")
    canvas.setFont("ProbeBody", 8)
    canvas.drawCentredString(306, 740, "contato@example.test | Perfil demonstrativo")
    def heading(y, title):
        canvas.setFillColorRGB(.1, .3, .5)
        canvas.setFont("ProbeBold", 9)
        canvas.drawString(34, y, title)
        canvas.setStrokeColorRGB(.6, .6, .6)
        canvas.setLineWidth(.4)
        canvas.line(34, y-3, 578, y-3)
        canvas.setFillColorRGB(0, 0, 0)
    def category(y):
        canvas.setFont("ProbeBold", 8)
        canvas.drawString(34, y, "Bancos de Dados: ")
        x = 34 + pdfmetrics.stringWidth("Bancos de Dados: ", "ProbeBold", 8)
        canvas.setFont("ProbeBody", 8)
        body = "SQL Server, MySQL, modelagem de dados."
        if long_body:
            body = "SQL Server, MySQL, " + "modelagem " * 7 + "dados."
        canvas.drawString(x, y, body)
    heading(706, "RESUMO")
    canvas.setFont("ProbeBody", 8)
    canvas.drawString(34, 691, "Profissional de tecnologia. Conteudo inteiramente sintetico.")
    heading(663, "HABILIDADES TECNICAS")
    category(648)
    if duplicate:
        category(634)
    if multiline:
        canvas.drawString(34, 634, "continuacao da categoria anterior.")
    heading(600, "EXPERIENCIA PROFISSIONAL")
    canvas.setFont("ProbeBody", 8)
    canvas.drawString(34, 583, "Empresa Exemplo - Desenvolvimento de APIs e testes.")
    heading(550, "FORMACAO ACADEMICA")
    canvas.setFont("ProbeBody", 8)
    canvas.drawString(34, 533, "Instituicao Exemplo - Tecnologia.")
    if link_y is not None:
        canvas.linkURL("https://example.test", (34, link_y - 2, 190, link_y + 9))
    if two_pages:
        canvas.showPage()
        canvas.drawString(34, 750, "Segunda pagina sintetica")
    canvas.save()
    return output.getvalue()


class LayoutProbeTests(unittest.TestCase):
    def plan(self, source=None, skill="PostgreSQL", category="Bancos de Dados"):
        return plan_skill_insertion(source or synthetic_resume(), "HABILIDADES TECNICAS", category, skill)

    def apply(self, source, skill="PostgreSQL", confirmed=True):
        return apply_skill_insertion(source, "HABILIDADES TECNICAS", "Bancos de Dados", skill, confirmed=confirmed)

    def test_preserves_page_fonts_graphics_and_every_other_operation(self):
        source = synthetic_resume()
        plan = self.plan(source)
        result = self.apply(source)
        original = PdfReader(io.BytesIO(source))
        adapted = PdfReader(io.BytesIO(result))
        self.assertEqual(len(adapted.pages), 1)
        self.assertEqual(tuple(original.pages[0].mediabox), tuple(adapted.pages[0].mediabox))
        before = ContentStream(original.pages[0].get_contents(), original).operations
        after = ContentStream(adapted.pages[0].get_contents(), adapted).operations
        self.assertEqual(len(before), len(after))
        for index, (first, second) in enumerate(zip(before, after)):
            if index != plan.operation_index:
                self.assertEqual(first, second)
        for name, reference in original.pages[0]["/Resources"]["/Font"].items():
            other = adapted.pages[0]["/Resources"]["/Font"][name].get_object()
            self.assertEqual(reference.get_object().get("/BaseFont"), other.get("/BaseFont"))
            if "/FontDescriptor" in other:
                self.assertEqual(reference.get_object()["/FontDescriptor"]["/FontFile2"].get_data(), other["/FontDescriptor"]["/FontFile2"].get_data())
        with pdfplumber.open(io.BytesIO(result)) as pdf:
            text = pdf.pages[0].extract_text()
            self.assertIn("SQL Server, MySQL, modelagem de dados, PostgreSQL.", text)
            self.assertEqual(text.count("PostgreSQL"), 1)

    def test_confirmation_is_required(self):
        with self.assertRaises(ReviewRequired):
            self.apply(synthetic_resume(), confirmed=False)

    def test_duplicate_is_not_added(self):
        with self.assertRaises(ReviewRequired):
            self.plan(skill="mysql")

    def test_missing_category_is_not_appended_elsewhere(self):
        with self.assertRaises(ReviewRequired):
            self.plan(category="Cloud")

    def test_ambiguous_category_requires_review(self):
        with self.assertRaises(ReviewRequired):
            self.plan(synthetic_resume(duplicate=True))

    def test_overflow_does_not_shrink_or_create_page(self):
        with self.assertRaisesRegex(ReviewRequired, "Não cabe"):
            self.plan(skill="PostgreSQL " * 30)

    def test_multiline_category_requires_review(self):
        with self.assertRaises(ReviewRequired):
            self.plan(synthetic_resume(multiline=True))

    def test_missing_glyph_does_not_substitute_font(self):
        with self.assertRaisesRegex(ReviewRequired, "caracteres"):
            self.plan(skill="Tecnologia 漢")

    def test_multiple_pages_require_review(self):
        with self.assertRaises(ReviewRequired):
            self.plan(synthetic_resume(two_pages=True))

    def test_only_changes_the_named_section(self):
        with self.assertRaises(ReviewRequired):
            plan_skill_insertion(synthetic_resume(), "FORMACAO ACADEMICA", "Bancos de Dados", "PostgreSQL")

    def test_rejects_paragraphs_as_skills(self):
        with self.assertRaises(ReviewRequired):
            self.plan(skill="PostgreSQL\nTenho experiencia")

    def test_header_links_are_preserved(self):
        adapted = PdfReader(io.BytesIO(self.apply(synthetic_resume(link_y=740))))
        self.assertEqual(len(adapted.pages[0]["/Annots"]), 1)
        self.assertEqual(adapted.pages[0]["/Annots"][0].get_object()["/A"]["/URI"], "https://example.test")

    def test_links_on_changed_row_require_review(self):
        with self.assertRaises(ReviewRequired):
            self.plan(synthetic_resume(link_y=648))

    def test_cli_does_not_write_unconfirmed_or_overwrite_original(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source.pdf"
            source.write_bytes(synthetic_resume())
            original = source.read_bytes()
            target = Path(directory) / "adapted.pdf"
            args = ["probe", str(source), "--section", "HABILIDADES TECNICAS", "--category", "Bancos de Dados", "--skill", "PostgreSQL", "--output"]
            with contextlib.redirect_stdout(io.StringIO()):
                with patch.object(sys, "argv", args + [str(target)]):
                    self.assertEqual(main(), 2)
                self.assertFalse(target.exists())
                with patch.object(sys, "argv", args + [str(source), "--confirmed"]):
                    self.assertEqual(main(), 2)
            self.assertEqual(source.read_bytes(), original)


if __name__ == "__main__":
    unittest.main()
