# WORKFLOW.md

# Yaqoot Development Workflow

This document defines the mandatory workflow for all AI coding agents contributing to the Yaqoot Perfume Store project.

These rules apply to every implementation unless the user explicitly overrides them.

---

# Instruction Priority

Always follow instructions in the following order:

1. Current User Request
2. CLAUDE.md
3. PROJECT_STATUS.md
4. WORKFLOW.md
5. Other referenced documentation

If instructions conflict, the higher priority instruction always wins.

# Core Principle

Always prefer:

- Production quality
- Small incremental tasks
- Predictable architecture
- Minimal scope
- Reuse over duplication

Never sacrifice maintainability for speed.

---

# Production Mindset

Every implementation should be treated as production code.

Avoid:

- Temporary solutions
- Demo implementations
- Placeholder logic
- Fake data
- Experimental code

Implement only production-ready solutions.

# Analysis Before Implementation

Before implementing any task:

1. Understand the request.
2. Inspect the existing implementation.
3. Identify reusable code.
4. Confirm the required scope.
5. Only then begin implementation.

Never implement first and analyze later.

# Task Classification

Every task belongs to one of the following categories.

## 1. UI Redesign

Purpose:

Improve the presentation layer without changing behavior.

Allowed:

- Razor Views
- CSS
- JavaScript
- Layout improvements

Not allowed:

- Business logic changes
- Database changes
- New features

---

## 2. Feature Enhancement

Purpose:

Extend an existing workflow with missing functionality.

Allowed:

- New Views
- New Controller Actions
- Minimal backend additions
- ViewModels
- Navigation updates

Requirements:

- Reuse existing services.
- Preserve architecture.
- Avoid duplicated business logic.

---

## 3. Module Audit

Purpose:

Inspect an existing module.

Do not:

- Modify files
- Generate code

Instead report:

- Existing functionality
- Existing Views
- Missing Views
- Essential Features
- Optional Enhancements

---

## 4. Bug Fix

Purpose:

Fix incorrect behavior.

Do not redesign UI unless required by the fix.

---

## 5. Refactoring

Purpose:

Improve internal code quality.

Must not:

- Change behavior
- Introduce new features

---

## 6. Technical Debt

Purpose:

Resolve accumulated engineering issues.

Examples:

- Hardcoded values
- Duplicate logic
- Inconsistent naming
- Obsolete code

Technical Debt should normally be handled in a dedicated sprint.

---

# Module Workflow

Every feature module follows this lifecycle.

Module Audit

â†“

Planning

â†“

Implementation

â†“

Module Completion Audit

â†“

Approval

â†“

Move to next module

Never skip steps.

---

# Module Completion Rules

A module is NOT complete after only implementing:

- Index
- Create
- Edit

Before reporting completion:

Inspect the corresponding Controller.

Identify:

- All View-returning Actions
- Partial Views
- View Components
- User workflows

If additional Views exist:

List them.

Do NOT redesign them unless requested.

If no remaining work exists:

Report:

Module Complete âœ…

---

# Essential vs Optional Features

Always classify missing functionality.

## Essential Features

Required for normal operation.

Examples:

- Order Details
- User Details

## Optional Enhancements

Useful but can be deferred.

Examples:

- Export
- Timeline
- Print
- Bulk Actions
- Dashboard analytics

---

# Technical Debt Policy

Do not mix Technical Debt with UI tasks.

If Technical Debt is discovered:

Report it.

Do not fix it unless explicitly requested.

---

# Project Scale

Yaqoot targets:

Small-to-medium commercial businesses.

Avoid enterprise-level complexity.

Prefer simplicity over excessive configurability.

---

# Implementation Rules

Modify only files required for the task.

Preserve:

- MVC
- Routing
- Authorization
- Validation
- Existing business rules

Reuse existing code whenever possible.

Avoid duplicate implementations.

---

# Completion Report

Every completed task must include:

## Modified Files

## New Files

## Backend Changes

## Remaining Views

## Critical Fixes

## Essential Features

## Optional Enhancements

## Suggested Git Commit Message

---

# Updating PROJECT_STATUS.md

Update PROJECT_STATUS.md only when:

- A feature module is completed.
- A project phase changes.
- Project architecture changes.

Do not update PROJECT_STATUS.md for:

- CSS tweaks
- Minor UI polish
- Small bug fixes
- Refactoring

---

# Phase Completion

Before closing a project phase:

Perform a complete phase audit.

Verify:

- Navigation
- Controllers
- Views
- Shared components
- CSS
- JavaScript

Report:

- Critical Issues
- Optional Improvements

Only then declare the phase complete.

---

# Decision Making

When multiple implementation options exist:

Prefer the option that:

- Preserves existing architecture.
- Introduces the least complexity.
- Requires the smallest maintenance cost.
- Produces the highest long-term stability.

# Scope Discipline

Stay within the approved task scope.

If you discover:

- Missing functionality
- Better implementation opportunities
- Technical debt
- Architecture improvements

Do not implement them automatically.

Instead:

- Report them.
- Classify them.
- Wait for explicit approval.

Never expand the scope without approval.
