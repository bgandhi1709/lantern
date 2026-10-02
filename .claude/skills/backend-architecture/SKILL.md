---
name: backend-architecture
description: Lantern's .NET code map, a suffix glossary plus recipes. Use before adding or changing C# under apps/ or libs/ (an endpoint, resource, service, repository, entity, store, event handler, interactor, validator, attribute, setting or external client), or when unsure which layer, suffix or base class a class gets.
---

# Backend architecture

Lantern's .NET code mirrors the user's `wf` architecture (ADR-0005). Every class has a **suffix** that fixes its
meaning, layer, base class and registration; every change follows a **recipe**.

1. Read [`docs/architecture/recipes.md`](../../../docs/architecture/recipes.md) and pick the recipe(s) from its decision
   list. Done when you can name each recipe the change needs.
2. For every class the recipe has you add or change, look up its suffix in
   [`docs/architecture/code-glossary.md`](../../../docs/architecture/code-glossary.md) and open the reference class it
   names. Done when every new class has a suffix from the glossary, a folder and a registration.
3. If the change needs a kind of class the glossary does not list, or a step that contradicts a recipe, stop: that is
   a divergence (`docs/agents/workflow.md`). Ask the user, then add the kind to the glossary in the same PR.
4. Follow the recipe's steps in order, then its **Done** list. The architecture tests
   (`tests/lantern-api/Lantern.Api.Tests/Architecture`) must pass unchanged; never edit them to make a change pass.
