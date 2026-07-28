# 1. Record architecture decisions

Date: 2026-07-28
Status: Accepted

## Context

Onyx has several open technical decisions (final name, ONNX model choice,
multi-face support, installer packaging) and non-obvious constraints (MF virtual
camera requires Windows 11 and out-of-process frame delivery).

## Decision

We will keep lightweight Architecture Decision Records (ADRs) under
`docs/decisions/`, one per significant choice, numbered sequentially.

## Consequences

Decisions and their rationale stay discoverable as the project grows, instead of
living only in chat history or code comments.
