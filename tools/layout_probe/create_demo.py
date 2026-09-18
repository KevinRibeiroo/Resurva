"""Creates only synthetic preview artifacts; never reads a user's resume."""
import argparse
from pathlib import Path

from layout_probe import apply_skill_insertion
from test_layout_probe import synthetic_resume


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    args.directory.mkdir(parents=True, exist_ok=True)
    source = synthetic_resume()
    adapted = apply_skill_insertion(source, "HABILIDADES TECNICAS", "Bancos de Dados", "PostgreSQL", confirmed=True)
    for name, data in (("original-sintetico.pdf", source), ("adaptado-sintetico.pdf", adapted)):
        with (args.directory / name).open("xb") as output:
            output.write(data)


if __name__ == "__main__":
    main()
