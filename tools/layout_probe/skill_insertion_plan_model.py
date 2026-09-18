from dataclasses import dataclass


@dataclass(frozen=True)
class SkillInsertionPlanModel:
    operation_index: int
    encoded_text: bytes
    page_width: float
    page_height: float
    original_width: float
    adapted_width: float
    available_width: float
