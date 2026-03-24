# IL2CPP Codegen Pattern Decompiler Design

Date: 2026-03-23
Status: Draft approved in conversation, pending written-spec review
Scope: `Cpp2IL.Core` `isil_to_cil`

## 1. Summary

`isil_to_cil` should stop evolving as a general native-to-managed lifter and instead become an IL2CPP codegen pattern decompiler.

The output target is not "best effort recreation of original Mono IL". The output target is:

- verifier-safe CIL
- semantically close to IL2CPP runtime behavior
- explicit about IL2CPP runtime protocol where required
- buildable without depending on the original native binary at runtime

The design keeps the current CFG / SSA / stackification / resolver substrate, but demotes it to a lowering backend. The primary semantic layer becomes IL2CPP pattern recognition plus IL2CPP-aware semantic reconstruction.

## 1.1 Status Quo

Today, `PatternConverter` is the practical center of `isil_to_cil`.

Its responsibilities are mixed:

- recognize a subset of level-1 method patterns
- emit direct CIL for simple cases
- route structured-control-flow methods through CFG / SSA / stackification
- invoke intrinsic-backed fallback
- decide when semantic recovery degrades to intrinsic or stub-like output

This design keeps that implementation base but changes its role. `PatternConverter` should become a coordinator and lowering entry point, not the primary semantic recognizer for IL2CPP-generated code.

## 2. Goals

### Primary goals

- Recognize stable IL2CPP codegen idioms as first-class semantics.
- Preserve IL2CPP runtime behavior in emitted CIL instead of forcing everything into pseudo-original IL.
- Keep generated CIL verifier-safe and buildable.
- Improve ground-truth coverage and exact-token/opcode alignment in high-value source-owned buckets.

### Non-goals

- Full reconstruction of original pre-IL2CPP Mono IL.
- Runtime dependence on the original native binary.
- Converting `Cpp2IL.Runtime` into a full libil2cpp reimplementation.
- Solving all IL2CPP codegen families in the first phase.

## 3. Design Principles

### 3.1 Semantic priority

When IL2CPP-specific runtime protocol conflicts with "clean-looking" managed IL, prefer preserving IL2CPP semantics.

### 3.2 Pattern-first, lowering-second

The system should first ask "which IL2CPP codegen idiom is this?" and only then ask "how do I lower it safely to CIL?".

### 3.3 Conservative direct emission

High-confidence patterns may emit direct CIL. Ambiguous patterns must fall back to explicit runtime shim helpers rather than guessed managed forms.

### 3.4 No native-runtime dependency

All emitted code and runtime helpers must work from:

- recovered assemblies
- existing metadata mappings already available during recovery
- generated helper code in `Cpp2IL.Runtime`

No runtime dependence on the original game binary, registration tables in memory, or native address lookups is allowed.

## 4. Architecture

The system should evolve into four layers.

### 4.1 IL2CPP Pattern Discovery

New subsystem under `Cpp2IL.Core/ISILToCil/IL2CPP/Patterns`.

Responsibilities:

- recognize IL2CPP codegen idioms from ISIL plus recovered metadata context
- classify call shapes, delegate construction shapes, runtime metadata initialization, dispatch forms, and generic context access
- produce structured pattern nodes, not emitted IL

The recognizer input must be explicit rather than ad-hoc. It should consume a fixed `Il2CppPatternContext` containing:

- filtered ISIL for the current method
- `MethodAnalysisContext` for the enclosing method
- declaring type, return type, and parameter metadata
- existing conservative resolver services
- metadata-binding results already available during recovery
- basic structural facts already computed earlier, such as CFG/basic-block information when available
- optional Unity / IL2CPP version facts known to the app context

The recognizer should not reach directly into arbitrary global state. Any new dependency should be added to `Il2CppPatternContext`.

Example output kinds:

- `DirectManagedCall`
- `InvokerCall`
- `VirtualDispatch`
- `InterfaceDispatch`
- `DelegateConstruction`
- `EventSubscription`
- `RGCTXDataAccess`
- `MetadataInitialization`

### 4.1.1 Version-aware pattern dispatch

IL2CPP codegen templates vary across Unity versions and codegen eras. Pattern recognition therefore must not assume one global shape.

The pattern layer should define:

- `Il2CppPatternProfile`
- `Il2CppPatternProfileResolver`

The resolver should derive a coarse profile from available inputs such as:

- Unity version family when known
- IL2CPP metadata version when known
- feature toggles inferred from recovered runtime shape

Patterns should then be authored either as version-agnostic recognizers or profile-specialized recognizers selected through that resolver. Phase 1 does not require full historical coverage, but the dispatch point must exist now.

### 4.2 IL2CPP Semantic Reconstruction

New subsystem under `Cpp2IL.Core/ISILToCil/IL2CPP/Semantics`.

Responsibilities:

- turn recognized pattern nodes into verifier-safe CIL emission plans
- decide between direct CIL and `Cpp2IL.Runtime` helper calls
- preserve hidden method metadata and runtime protocol when needed

This layer defines semantic intent. It does not own local allocation, CFG plumbing, or stack safety mechanics.

### 4.2.1 Semantic plan IR

The handoff between semantic reconstruction and lowering must be explicit.

Introduce an internal intermediate representation, tentatively `Il2CppSemanticPlan`, composed of plan nodes such as:

- `PlanLoadArgument`
- `PlanLoadField`
- `PlanStoreField`
- `PlanCallDirect`
- `PlanCallInvoker`
- `PlanConstructDelegate`
- `PlanInitializeMetadata`
- `PlanBranch`
- `PlanReturn`

This IR is not raw ISIL and not raw CIL. It represents IL2CPP-aware semantic operations that the lowering backend can materialize safely.

Rules:

- semantic reconstruction may build plan nodes only
- verifier-safe lowering may translate plan nodes into CIL, locals, and branches
- semantic reconstruction must not directly append `CilInstruction`s except in tightly-scoped compatibility bridges scheduled for later removal

### 4.3 Verifier-Safe Lowering Backend

Existing substrate remains in place:

- CFG builder
- SSA builder
- stackification
- local allocation
- materialization
- conservative resolver

Responsibilities:

- lower semantic plans into legal method bodies
- preserve stack correctness
- manage locals, branches, joins, and fallback

This layer should no longer be the primary place where IL2CPP semantics are discovered.

`Cpp2IL.Runtime` reference injection happens between semantic reconstruction and final lowering, when semantic-plan nodes are bound to concrete helper references for the current module.

### 4.4 `Cpp2IL.Runtime` as IL2CPP Semantic Shim

`Cpp2IL.Runtime` should be expanded from fallback-only helper library into an IL2CPP semantic shim.

Responsibilities:

- represent IL2CPP runtime-only concepts in managed form
- host helper APIs for dispatch, delegate construction, generic context access, and metadata initialization
- make uncertain IL2CPP semantics explicit and stable in recovered output

This is an explicit trade-off. Recovered assemblies are allowed to take a hard dependency on `Cpp2IL.Runtime.dll`.

Phase 1 accepts that dependency as the default and supported mode. A future runtime-free mode is allowed only for high-confidence direct-CIL patterns, but is not required for this phase and must not distort phase-1 design decisions.

## 5. First-Phase Pattern Families

Phase 1 intentionally limits scope to three families with the highest current value.

### 5.1 Delegate Construction and Event Wiring

Motivation:

- current source-owned `OnEnable` / `OnDisable` methods still regress to `IntrinsicThrow` too often
- IL2CPP delegate construction is highly templated

Expected recovery:

- recognize delegate constructor idioms
- recognize event subscribe/unsubscribe idioms
- emit direct managed event/delegate code when the shape is high-confidence
- otherwise emit `Cpp2IL.Runtime` delegate helpers instead of generic unresolved call fallback

### 5.2 Invoker Call

Motivation:

- IL2CPP uses `invoker_method` as a normal runtime protocol, not merely as a fallback oddity

Expected recovery:

- recognize invoker-based calls as a distinct semantic family
- emit `Cpp2IL.Runtime.Il2CppInvoker` helpers when direct managed call emission is not justified
- stop treating these patterns as generic unresolved indirect calls

### 5.3 Metadata Initialization

Motivation:

- IL2CPP emits explicit metadata initialization scaffolding
- current recovery overstates this scaffolding as business logic

Expected recovery:

- recognize `il2cpp_codegen_initialize_runtime_metadata*` style idioms
- fold them into explicit metadata shim helpers
- reduce noise in recovered bodies while preserving semantics

### 5.4 Composition model for phase-1 families

The phase-1 families are not independent. A single method may contain all three.

Expected composition order:

1. metadata initialization recognition
2. delegate construction / event wiring recognition
3. invoker-call recognition

The recognizer should therefore support multi-pattern segmentation within one method body rather than assuming one method maps to one family.

Phase-1 composition rules:

- metadata-init may wrap or precede other families
- delegate construction may contain direct or invoker-mediated target resolution
- invoker-call recognition may occur inside a delegate or event-wiring plan

If segmentation is ambiguous, the method should degrade to lower-confidence semantic plans or existing fallback rather than forcing a single-family interpretation.

## 6. Deferred Pattern Families

These are intentionally deferred until after the first phase:

- rgctx and generic sharing families
- virtual and interface dispatch families
- constrained call and valuetype `this` adjustment
- icall / pinvoke / internal call families
- array special invokers
- reverse pinvoke and marshaling wrappers
- exception / execution-engine helper families
- static constructor and class-init protocol families beyond simple metadata-init scaffolding

They remain in scope for the long-term architecture but not the first execution plan.

## 7. `Cpp2IL.Runtime` Additions

Phase 1 requires the runtime shim to grow in targeted ways.

### 7.1 Method-handle representation

Add a stable managed representation for hidden `RuntimeMethod*` / `MethodInfo`-like concepts that can be constructed from existing recovery metadata.

### 7.2 Invoker helpers

Add helper entry points that model invoker protocol explicitly, for example:

- `InvokeVoid`
- `Invoke<T>`
- shape-specific overloads as needed for verifier-safe emission

### 7.3 Delegate helpers

Add helper entry points for:

- delegate construction
- closed/open binding
- event-oriented helper paths when a direct managed translation is unsafe

### 7.4 Metadata initialization helpers

Add helper entry points for:

- metadata initialization
- type info retrieval
- string literal and metadata-backed object retrieval where needed

These helpers must remain independent from the original native binary at runtime.

## 8. Integration Strategy

The migration should not rewrite the whole backend in one step.

### Recommended strategy

Adopt "small integration now, clean architecture target":

- immediately integrate an IL2CPP pattern-recognition stage in front of the current general conversion path
- write all new code using the target subsystem boundaries (`IL2CPP/Patterns`, `IL2CPP/Semantics`, `IL2CPP/RuntimeModel`)
- keep `PatternConverter` as the short-term coordinator
- later reduce `PatternConverter` to a narrow orchestration and lowering role

### Why this strategy

- it avoids another large risky rewrite
- it validates IL2CPP-specific value quickly
- it avoids piling more semantic guesswork into `PatternConverter`

## 9. Data Flow

Target flow:

`ISIL -> CFG/basic facts -> IL2CPP pattern recognizer -> semantic plan -> runtime-helper binding -> existing lowering backend -> verifier-safe CIL`

Current flow is too close to:

`ISIL -> generic instruction lifting -> fallback`

The purpose of this design is to invert that priority for IL2CPP workloads.

## 10. Error Handling and Fallback Policy

### High-confidence patterns

- may emit direct CIL
- may bypass generic fallback if semantic certainty is sufficient

### Medium-confidence patterns

- should emit explicit `Cpp2IL.Runtime` shim calls
- should preserve hidden IL2CPP runtime semantics instead of forcing guessed direct IL

### Low-confidence patterns

- continue to use existing conservative fallback and stub mechanisms
- must not regress back to "first candidate" binding or silent semantic guessing

## 11. Testing Strategy

### Focused tests

Add unit and focused integration tests for:

- delegate construction pattern recognition
- event subscription and unsubscription paths
- invoker call recognition and helper emission
- metadata initialization recognition and helper emission

Add explicit negative tests for:

- near-miss patterns that must not be recognized
- mixed-family methods where segmentation is ambiguous and must degrade conservatively
- version-profile mismatches that must not silently bind to the wrong recognizer

### Ground-truth tests

Use Platformer as the first acceptance sample, but keep the evaluator generic.

Primary checkpoints:

- source-owned high-value buckets improve in non-stub and exact-token/opcode metrics
- exact instruction-token match does not regress
- emitted methods remain verifier-safe
- `IntrinsicThrow` usage drops in target families

In addition, maintain a regression anchor suite of already-good recoveries outside the target families. Phase 1 must explicitly hold the line on:

- simple getters/setters
- already-correct field access patterns
- existing structured buckets with non-zero exact-token matches
- conservative resolver ambiguity behavior introduced by the resolver redesign

### Success metrics

Priority order:

1. exact token/opcode match and target-bucket coverage improvement
2. verifier-safe, buildable recovered assemblies
3. improved practical executability where it does not conflict with (1)

### Diagnostics

Pattern recognition and semantic emission should produce structured diagnostics in the conversion report.

Required minimum diagnostics:

- recognized pattern kind
- confidence tier
- profile used for recognition
- reason for medium-confidence shim emission instead of direct CIL
- reason for low-confidence fallback when a target-family recognizer partially matched

These diagnostics are required both for developer iteration and for users inspecting recovered output quality.

## 12. Risks

### Risk: runtime shim overgrowth

If `Cpp2IL.Runtime` grows without clear boundaries, it may become a second compiler backend encoded as helper calls.

Mitigation:

- direct CIL when the pattern is truly high-confidence
- helpers only for IL2CPP runtime protocol that cannot be safely flattened

### Risk: pattern explosion

IL2CPP has many codegen families; trying to model all of them at once will stall the redesign.

Mitigation:

- phase the work
- keep phase 1 restricted to three pattern families

### Risk: mixing old and new responsibilities

If `PatternConverter` keeps absorbing semantic logic, architecture will regress.

Mitigation:

- new semantic logic must land in new IL2CPP-namespaced subsystems
- `PatternConverter` should only coordinate and lower

## 13. Acceptance Criteria for Phase 1

Phase 1 is considered successful when:

- IL2CPP pattern-recognition and semantic-emission layers exist and are wired into `isil_to_cil`
- `Cpp2IL.Runtime` contains the minimum helper surface for phase-1 families
- source-owned event/delegate-style methods no longer predominantly fall into generic unresolved fallback
- invoker-shaped calls are recognized distinctly from generic indirect calls
- metadata-init scaffolding is recognized and emitted through dedicated semantic handling
- Platformer ground-truth shows improvement or at minimum no regression in exact-token/opcode metrics for target buckets

Quantitative thresholds for phase 1:

- at least 70% of source-owned methods classified into the target phase-1 families must emit non-stub bodies
- at least 50% of source-owned delegate/event-wiring methods in the evaluation sample must stop using generic unresolved fallback
- invoker-shaped calls in the curated regression sample must be classified as `InvokerCall` in 100% of canonical test cases
- metadata-init idioms in the curated regression sample must be recognized in 100% of canonical phase-1 test cases
- exact instruction-token match across the non-target regression anchor suite must not decrease

These are phase-1 thresholds, not final-product goals.

## 14. Open Follow-Up After This Spec

After this spec is approved, the implementation plan should define:

- exact internal types and file layout for `IL2CPP/Patterns`, `IL2CPP/Semantics`, and `IL2CPP/RuntimeModel`
- phase-1 helper API surface in `Cpp2IL.Runtime`
- concrete first bucket samples to use as regression anchors
- phased refactoring of `PatternConverter` into coordinator-only responsibilities
