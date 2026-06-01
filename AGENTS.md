# FocusAnchor - Agent Instructions

## Project goal
Build a C# desktop app that helps users reduce procrastination by choosing, protecting, and reviewing focused attention sessions.

## Product principles
- The app should help the user own their attention.
- Do not split tasks into micro-goals.
- Do not create productivity pressure or shame.
- Prefer calm, minimal, low-friction interactions.
- The app should make it easy to start focusing, not to over-plan.
- Prefer local-first behavior.
- Do not add AI/API features unless explicitly requested.

## Core concepts
- FocusIntent: what the user wants to give attention to now.
- FocusSession: a timed block of attention.
- DistractionEntry: something that pulled attention away.
- AttentionReview: a short reflection after a session.

## Architecture
- Use C# and .NET.
- Keep domain logic in FocusAnchor.Core.
- Keep persistence in FocusAnchor.Data.
- Keep UI logic separate from domain rules.
- Domain behavior should be deterministic and testable.

## Definition of done
Before marking work complete:
- The app builds.
- Relevant tests pass.
- New domain behavior has tests.
- The change is explained briefly.
- No unrelated refactors.

## Commands
Use these commands when applicable:
- dotnet build
- dotnet test

## Working style
- For complex work, make a short plan before editing.
- Change one feature at a time.
- Prefer small commits/diffs.
- Do not introduce new packages without explaining why.