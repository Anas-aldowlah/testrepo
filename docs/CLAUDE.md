# CLAUDE.md

# Yaqoot AI Agent Entry Point

This file is the single starting point for every AI coding agent working on the Yaqoot repository.

It does not contain the full project rules. Instead, it explains how to begin work, which documents to read, and where authoritative guidance lives. Specialized documentation is the source of truth for its own topic and must be consulted before implementation decisions are made.

## 1. Project Overview

Yaqoot is a production-ready ASP.NET Core MVC e-commerce template for a premium perfume storefront and administrative dashboard.

The project is designed as a reusable commercial template for multiple future client deployments. Work on this repository must preserve long-term maintainability, extensibility, security, performance, accessibility, and clean separation between core template architecture and client-specific customization.

## 2. Project Goals

The project exists to provide a stable, reusable foundation for high-quality e-commerce deployments.

Core goals:

- Preserve a production-ready ASP.NET Core MVC architecture.
- Keep business data dynamic and backed by the configured persistence layer.
- Maintain a premium, consistent design system that can be branded for future clients.
- Encourage reusable modules, shared components, and predictable folder organization.
- Support future client customization without weakening the core template.
- Keep documentation synchronized with the actual project state.

## 3. How AI Agents Should Begin Any Task

Before making changes, an AI agent must:

1. Read the user request carefully and identify the task type.
2. Read `PROJECT_STATUS.md` to understand the current project state.
3. Read the specialized documentation relevant to the task.
4. Inspect only the project files needed to complete the requested work.
5. Search for existing patterns before creating new files, components, services, styles, or scripts.
6. Confirm whether `PROJECT_STATUS.md` or another documentation file must be updated after the task.

Agents must not rely on assumptions when a project rule is documented elsewhere. Consult the appropriate document first.

## 4. Documentation Reading Order

Use this reading order when starting work:

1. `PROJECT_STATUS.md`
2. `docs/WORKFLOW.md`
3. `docs/DECISIONS.md`
4. `docs/ARCHITECTURE.md`
5. `docs/DESIGN_SYSTEM.md`
6. `docs/FRONTEND_RULES.md`
7. `docs/BACKEND_RULES.md`
8. `docs/PHASES.md`

Read all documents when the task is broad, architectural, roadmap-related, or cross-cutting.

For focused tasks, read `PROJECT_STATUS.md`, `docs/WORKFLOW.md`, and the specialized document that governs the affected area.

## 5. Mandatory Workflow Before Implementation

Before implementation, agents must follow `docs/WORKFLOW.md`.

At minimum, agents must:

- Understand the current project phase and requested scope.
- Identify the affected layer or module.
- Review existing implementation patterns before adding new ones.
- Keep changes limited to the requested task.
- Avoid unrelated refactoring.
- Preserve dynamic data flow, routing, validation, authorization, and existing business behavior unless the user explicitly requests a change.

Workflow details belong in `docs/WORKFLOW.md`; do not duplicate them here.

## 6. Required Behavior During Implementation

During implementation, agents must use the specialized documentation as the authority for decisions:

- Architecture decisions: `docs/ARCHITECTURE.md`
- Design and visual consistency: `docs/DESIGN_SYSTEM.md`
- Razor, CSS, JavaScript, layouts, and frontend assets: `docs/FRONTEND_RULES.md`
- Controllers, services, models, ViewModels, validation, persistence, and configuration: `docs/BACKEND_RULES.md`
- Roadmap, phases, release planning, and deferred work: `docs/PHASES.md`
- Prior architectural decisions: `docs/DECISIONS.md`
- Current status and milestones: `PROJECT_STATUS.md`

Agents must favor existing patterns, reusable components, dependency injection, configuration-based behavior, and modular extension. Core template architecture should remain stable, and client-specific customization should be isolated according to the project documentation.

## 7. Required Completion Report

At the end of a task, agents must provide a concise completion report that includes:

- Summary of changes
- Modified files
- New files
- Deleted files
- Architectural or design impact, if any
- Backend or frontend limitations, if any
- Whether `PROJECT_STATUS.md` or other documentation was updated
- Suggested git commit message

If a category does not apply, state `None` or `No update required`.

The completion report should be brief and factual. Detailed workflow rules for task completion remain in `docs/WORKFLOW.md`.

## 8. Documentation Responsibilities

Documentation must stay aligned with the repository.

Agents should update documentation when a task changes:

- Project phase, status, roadmap, or milestone information.
- A significant architectural decision.
- A reusable module, service, component, or pattern.
- A design-system rule or reusable UI pattern.
- Frontend or backend development rules.
- External services, configuration strategy, or deployment assumptions.

Do not update documentation for minor implementation details, small bug fixes, formatting-only changes, or temporary work unless the user requests it.

When documentation conflicts, treat the specialized document for that topic as authoritative and report the inconsistency so it can be corrected.
