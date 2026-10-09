from __future__ import annotations

import re
from collections import Counter


STOP_WORDS = set("的了和是在有我你他她它与及或为对将把被这那一个我们可以通过进行使用等并而但也都就很更最" )


def split_sentences(text: str) -> list[str]:
    return [item.strip() for item in re.split(r"(?<=[。！？!?；;])\s*|\n+", text) if item.strip()]


def summarize(text: str, ratio: float = 0.25, mode: str = "核心要点") -> str:
    sentences = split_sentences(text)
    if not sentences:
        return ""
    characters = [char for char in text if "\u4e00" <= char <= "\u9fff" and char not in STOP_WORDS]
    frequency = Counter(characters)
    scored: list[tuple[float, int, str]] = []
    for index, sentence in enumerate(sentences):
        useful = [char for char in sentence if char in frequency]
        score = sum(frequency[char] for char in useful) / max(len(sentence), 1)
        if index == 0:
            score *= 1.08
        scored.append((score, index, sentence))
    if mode == "一句话总结":
        return max(scored)[2]
    count = max(1, min(len(sentences), round(len(sentences) * ratio)))
    selected = sorted(sorted(scored, reverse=True)[:count], key=lambda item: item[1])
    if mode == "核心要点":
        return "\n".join(f"{index}. {item[2]}" for index, item in enumerate(selected, 1))
    return "".join(item[2] for item in selected)

