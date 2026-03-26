# P-CIL v1.1 Formal Specification

**Status**: Draft v1.1
**Date**: 2026-03-25
**Based on**: v1 (2026-03-24), revised per P-CIL v1.1 Improvement Design (R2)
**Scope**: Recovery IR contract between P-Code and CIL for IL2CPP binary analysis

---

## Editorial Contract

This section is normative. All chapters in this specification MUST conform to the conventions defined here.

### Terminology

| Term | Definition |
|------|-----------|
| **resolved** | Recovery object with sufficient evidence, no ambiguity, ready for direct lowering |
| **partial** | Recovery object with some evidence but incomplete; carries what is known, marks what is missing |
| **unresolved** | Recovery object where evidence is insufficient; explicitly represented, not hidden |
| **carrier** | IL2CPP protocol object that transfers metadata through call dispatch (MethodInfo, RGCTXData, VirtualInvokeData, delegate fields, etc.) |
| **evidence anchor** | Traceable link from a recovery claim to its source (symbol, pattern, metadata, hybrid, manual) |
| **confidence** | Numeric 0..1 measure of evidence sufficiency for a recovery claim |
| **overlay** | IL2CPP-specific recovery rule that modifies an existing semantic domain chapter |
| **annex** | IL2CPP-specific protocol that spans multiple semantic domains; gets its own appendix |
| **host substrate** | The decompiler platform providing raw facts (Ghidra P-Code / HighFunction) |
| **fallback** | Conservative recovery action taken when evidence is insufficient for full resolution |
| **recognized-but-fallback** | State where protocol family is identified but evidence is insufficient for full lowering; observable, not hidden |
| **recovery object** | Any semantic unit that P-CIL attempts to reconstruct from host substrate evidence |
| **lowering** | Translation from P-CIL recovery objects to verifier-safe CIL |
| **protocol family** | A category of IL2CPP call dispatch with distinct carrier shapes and runtime behavior |
| **recovered** | A recovery object that has been classified into resolved, partial, or recognized-but-fallback state; actionable by downstream consumers |

### Normative Language

- **MUST / SHALL**: normative requirement; violation constitutes non-conformance
- **SHOULD**: normative recommendation; deviation requires documented justification
- **MAY**: permitted behavior; no obligation either way
- **Informative note**: background context, not binding; marked with `> Note:` blockquote
- **Open question**: unresolved design point; marked with `> OPEN:` blockquote

### Source Anchor Format

Two anchor types are used throughout this specification, always distinguished:

**IL2CPP source anchor** (ground truth from local IL2CPP source):
```
[Source: il2cpp/<relative-path>:<function-or-region>]
```

**Project evidence anchor** (project decision, observation, or empirical finding):
```
[Evidence: <doc-name>:<section-or-finding>]
```

**Anchor rule**: every normative assertion (MUST/SHALL) MUST be traceable to at least one source anchor. Traceability MAY be satisfied by either an inline anchor adjacent to the assertion OR an entry in the chapter's Source Anchors table (section N.9) that covers the section containing the assertion. Assertions that cannot be traced to any anchor — neither inline nor via the chapter anchor table — MAY only appear as `> OPEN:` or `> Provisional:` blocks.

### Evidence Levels

| Level | Confidence Range | Typical Source | Meaning |
|-------|-----------------|----------------|---------|
| **Definitive** | 0.95 -- 1.0 | Symbol + metadata match | Recovery is certain |
| **Strong** | 0.80 -- 0.94 | Pattern + metadata corroboration | Recovery is highly likely |
| **Moderate** | 0.50 -- 0.79 | Pattern match only, or partial metadata | Recovery is plausible but needs validation |
| **Weak** | 0.20 -- 0.49 | Heuristic or positional inference | Recognized-but-fallback territory |
| **Insufficient** | 0.00 -- 0.19 | No meaningful evidence | Unresolved; MUST NOT lower as resolved |

### Unresolved Representation

Unresolved recovery objects use the canonical form:

```
Unresolved<Domain>(reason, evidence_so_far)
```

- `reason` is mandatory: explains WHY resolution failed
- `evidence_so_far` preserves partial observations collected before resolution failed
- Consumers MUST handle unresolved forms without crashing or aborting

### Overlay vs Annex Decision Rule

- **Overlay**: use when IL2CPP changes the recovery rules of an existing semantic domain. Written as a subsection within that domain's chapter.
- **Annex**: use when IL2CPP introduces a protocol concept that spans multiple semantic domains. Written as a separate appendix, referenced from relevant domain chapters.

### Chapter Structure Convention

Every chapter follows this skeleton (sections scale to complexity):

1. **Purpose** -- what this chapter covers and why it exists in P-CIL
2. **Semantic Target** -- what CIL-level semantic object(s) this chapter aims to recover (brief anchor, not ECMA-335 restatement)
3. **Evidence Sources** -- what host substrate evidence supports recovery in this domain
4. **Recovery Rules** -- how recovery proceeds; per-rule: precondition, evidence requirement, action, post-state
5. **Recovery Forms** -- resolved, partial, unresolved representations
6. **Confidence And Ambiguity** -- how confidence is assessed; when ambiguity arises
7. **Lowering Obligations** -- what a lowering pass MUST do with each recovery form
8. **IL2CPP Overlay / Annex References** -- IL2CPP-specific modifications; cross-references
9. **Source Anchors** -- table of anchors used in this chapter

Annexes adapt section names to their content while preserving the same spirit.

---

## Chapter 1: Definition And Scope

### 1.1 Purpose

This chapter defines P-CIL, establishes the scope of this specification, and fixes the terminology used throughout all subsequent chapters.

### 1.2 Semantic Target

**Definition.** P-CIL is an intermediate representation based on P-Code evidence, whose purpose is to recover CIL-level semantics. It is positioned between P-Code and CIL, and it explicitly carries evidence, confidence, and unresolved or partial recovery state.

#### 1.2.1 P-CIL Is

- **A recovery-oriented IR.** P-CIL exists to recover managed semantics from native evidence; it is not an execution model.
- **Rooted in P-Code or HighFunction evidence.** Every recovery object traces back to facts supplied by the **host substrate** (Ghidra decompiler P-Code / HighFunction).
- **Designed to recover CIL-level semantics.** The target semantic domain is ECMA-335 CIL, as emitted by IL2CPP-compiled binaries.
- **Able to carry partial, ambiguous, and unresolved state explicitly.** A recovery object MUST be representable as **resolved**, **partial**, or **unresolved**; no silent data loss is permitted.
- **Aligned to IL2CPP reality.** Where IL2CPP introduces protocol-specific evidence (hidden method info, RGCTX channels, invoker ABI, delegate protocol), P-CIL MUST model that evidence as first-class structure, not as ad-hoc annotation.

#### 1.2.2 P-CIL Is NOT

- **Just annotated P-Code.** P-CIL performs semantic lifting; it does not merely tag P-Code operations with CIL labels.
- **Just an IL2CPP protocol table.** P-CIL covers the full CIL semantic surface, not only IL2CPP-specific dispatch patterns.
- **Just a lowering plan to verifier-safe CIL.** Lowering is a downstream consumer of P-CIL (Chapter 10), not the IR itself.
- **A rewrite of ECMA-335.** P-CIL reuses ECMA-335 semantic categories where they apply and extends them only where IL2CPP codegen introduces structure that ECMA-335 does not describe.

### 1.3 Evidence Sources

This is a definition chapter. Evidence sources are referenced only to anchor scope decisions; per-domain evidence inventories appear in Chapters 4-9.

The scope and position of P-CIL are grounded in three independent evidence lines:

1. **Project practice** -- implementation experience with ISIL-based recovery revealed that semantic protocol facts are erased before recognition when the substrate is too flat. [Evidence: WHY_PIVOT:root-cause-substrate-erases-protocol-facts]
2. **IL2CPP source oracle** -- the local `il2cpp` codegen and runtime sources define the actual call-protocol families, carrier shapes, and metadata channels that P-CIL must model. [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:call-emission]
3. **Architecture decision** -- the ISIL retirement decision established the recovery pipeline position that P-CIL now occupies. [Evidence: ISIL_RETIREMENT:active-route-decision]

### 1.4 Recovery Pipeline Position

P-CIL occupies a specific position in the recovery pipeline. The end-to-end flow is:

```
native binary
    |
    v
Ghidra decompiler
    |
    v
P-Code / HighFunction facts          <-- host substrate
    |
    v
IL2CPP call-protocol facts            <-- carrier extraction
    |
    v
formal P-CIL recovery objects         <-- THIS SPECIFICATION
    |
    v
verifier-safe CIL lowering            <-- downstream consumer
```

Key boundaries:

- **Upstream boundary.** P-CIL consumes facts from the **host substrate** and from IL2CPP **carrier** extraction. It does not define how those facts are produced.
- **Downstream boundary.** P-CIL produces recovery objects that the **lowering** stage (Chapter 10) translates into verifier-safe CIL. P-CIL does not define the emitted bytecode encoding.
- **Protocol bridge.** Between host substrate facts and formal P-CIL, an IL2CPP call-protocol classification step identifies **protocol families** and extracts **carriers**. This step is specified in Chapter 5; Chapter 1 defines only the vocabulary.

[Evidence: DECISION_SUMMARY:intended-architecture-shape]

### 1.5 Scope Boundaries

1. **Recovery semantics, not execution semantics.** P-CIL defines what can be recovered from evidence and at what confidence. It does not define how recovered CIL executes at runtime.
2. **No host substrate quality guarantee.** P-CIL assumes the host substrate provides structurally valid P-Code / HighFunction output. The quality of that output is an external assumption; P-CIL recovery rules specify what they require, not how to fix a broken decompiler.
3. **Architecture-aware, not architecture-specific.** P-CIL recovery rules are defined over architecture-independent semantic categories. Architecture-dependent evidence extraction (e.g., x64 vs ARM64 calling conventions) is isolated to extraction adapters outside the formal P-CIL layer, though P-CIL evidence objects MAY record architecture context.
4. **Semantic domain coverage.** Each subsequent chapter covers one semantic domain (e.g., call dispatch, type resolution, control flow). **Overlays** modify an existing domain chapter to account for IL2CPP-specific behavior. **Annexes** define IL2CPP-specific protocols that span multiple semantic domains.

### 1.6 Recovery Forms

Recovery objects are formally defined in Chapter 3. For the purposes of this chapter, the following summary applies.

Every P-CIL recovery object MUST be in exactly one of three states:

| State | Meaning |
|---|---|
| **resolved** | Sufficient evidence, no ambiguity; ready for direct **lowering**. |
| **partial** | Some evidence present but incomplete; requires further analysis or explicit annotation before lowering. |
| **unresolved** | Evidence is insufficient; the object is explicitly represented, never hidden or silently dropped. |

A conforming P-CIL producer MUST NOT emit a **resolved** object when evidence supports only **partial** or **unresolved** status. Conservative classification is a normative requirement, not a quality recommendation.

### 1.7 Confidence And Ambiguity

The confidence and ambiguity model is formally defined in Chapter 3. This chapter establishes only the design principle:

- Every recovery object MUST carry a confidence score and at least one evidence anchor linking it to the host substrate fact(s) that justified its classification.
- Ambiguity -- where evidence supports more than one classification -- MUST be represented explicitly, not resolved by silent heuristic choice.

### 1.8 Lowering Obligations

Lowering is formally defined in Chapter 10. This chapter establishes only the contract boundary:

- A **resolved** P-CIL recovery object MUST be lowerable to verifier-safe CIL by a conforming lowering implementation.
- A **partial** recovery object SHOULD be lowerable with explicit fallback or diagnostic annotation.
- An **unresolved** recovery object MUST produce a well-defined fallback representation (e.g., intrinsic stub, diagnostic comment) rather than invalid CIL.

P-CIL does not prescribe a specific lowering implementation. It prescribes the semantic contract that any lowering implementation MUST satisfy.

### 1.9 Source Anchors

| Anchor | Reference |
|---|---|
| [Evidence: ISIL_RETIREMENT:active-route-decision] | ISIL retirement and active route decision |
| [Evidence: WHY_PIVOT:root-cause-substrate-erases-protocol-facts] | Root cause: substrate erases protocol facts |
| [Evidence: DECISION_SUMMARY:intended-architecture-shape] | Intended architecture shape |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:call-emission] | IL2CPP codegen call-protocol families |
| [Source: il2cpp/Unity.IL2CPP/MethodSignatureWriter.cs:NeedsHiddenMethodInfo] | IL2CPP hidden method info contract |
| [Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:metadata-access] | IL2CPP runtime metadata access |

---

## Chapter 2: Host Substrate Contract

### 2.1 Purpose

This chapter defines what P-CIL requires from its **host substrate** -- the decompiler platform that provides raw facts from which recovery proceeds. The host substrate is an explicit external dependency, not a component that P-CIL controls or guarantees.

### 2.2 Semantic Target

The semantic target is the **boundary contract** between the host substrate and the P-CIL recovery layer: what the substrate MUST provide, what quality assumptions P-CIL makes, and how recovery degrades when substrate quality is poor.

### 2.3 Host Substrate Requirements

#### 2.3.1 Control Flow Graph

The host substrate MUST provide a CFG for each function, including basic block boundaries, edge types (conditional, unconditional, call, return), and exception handler boundaries when recoverable.

#### 2.3.2 Def-Use Chains And Varnodes

The host substrate MUST provide variable definitions and use-chains sufficient to trace call-site argument materialization, carrier creation/consumption (Annex C), and value provenance.

#### 2.3.3 Call Sites With Parameters

The host substrate MUST identify call sites with: call target (direct or indirect), parameter list with positional mapping, and calling convention attribution. This is the most critical substrate fact for P-CIL.

#### 2.3.4 Address Spaces

The host substrate MUST distinguish code addresses, data addresses, and stack addresses.

### 2.4 Host Quality As External Assumption

P-CIL treats host substrate quality as an EXPLICIT EXTERNAL ASSUMPTION.

1. A conforming P-CIL implementation MUST NOT silently assume substrate perfection.
2. When substrate evidence is absent or malformed, recovery MUST degrade to partial or unresolved, not invent evidence.
3. Substrate quality is measurable but outside P-CIL's normative scope.

[Evidence: GHIDRA_FEASIBILITY:host-quality-boundary]

### 2.5 Architecture Independence

P-CIL recovery rules are architecture-independent. However, evidence quality is architecture-dependent: the same protocol family may survive decompilation with different fidelity on x64 vs ARM64.

A conforming P-CIL implementation MUST isolate architecture-dependent evidence extraction from architecture-independent recovery rules. P-CIL evidence objects MAY record architecture context but recovery rules MUST NOT branch on architecture.

[Evidence: LESSONS_LEARNED:lesson-11-cross-architecture-uniformity-cannot-be-assumed]

### 2.6 IL2CPPAnalyzer Enrichment

IL2CPPAnalyzer enriches the host substrate before P-CIL recovery begins.

**Provides**: method names/symbols, type layouts, ABI flags (`requiresMethodInfo`, `requiresRGCTXData`, `isSharedGeneric`), metadata globals, string literals, signature planning.

**Does NOT provide**: call-site protocol family classification, carrier lifecycle tracing, evidence composition or confidence scoring, data-flow analysis beyond Ghidra's native capabilities.

**Rule**: Metadata priors from IL2CPPAnalyzer are valuable corroborating evidence but MUST NOT substitute for call-site-derived evidence.

[Evidence: GHIDRA_FEASIBILITY:metadata-priors-cannot-replace-call-site-evidence]

### 2.7 Graceful Degradation

| Substrate Deficiency | Effect On Recovery |
|---|---|
| Missing CFG edges | State machine tracking may miss transitions |
| Imprecise call parameters | Protocol family confidence drops; carrier evidence may compensate |
| Failed calling convention | Hidden parameters not identifiable; ABI flags provide fallback prior |
| Unresolved indirect calls | `Unresolved<Call>` with preserved parameter observations |
| Collapsed checks | Recovery reports check as absent, not as never-emitted |

Degradation MUST be observable in diagnostics.

### 2.8 Source Anchors

| Anchor | Used In |
|---|---|
| [Evidence: GHIDRA_FEASIBILITY:host-quality-boundary] | 2.4 |
| [Evidence: GHIDRA_FEASIBILITY:metadata-priors-cannot-replace-call-site-evidence] | 2.6 |
| [Evidence: LESSONS_LEARNED:lesson-11-cross-architecture-uniformity-cannot-be-assumed] | 2.5 |

---

## Chapter 3: Core Recovery Model

### 3.1 Purpose

This chapter defines the core recovery model that all other chapters build upon. It establishes the taxonomy of recovery objects, the evidence and confidence framework, the state machine model for stateful IL2CPP domains, and the rules governing fallback and ambiguity. Every subsequent chapter inherits the vocabulary defined here.

### 3.2 Semantic Target

P-CIL recovery targets CIL-level semantic objects from host substrate evidence. The core model does not target any single CIL concept; instead it defines the **framework** through which all domain-specific recovery (calls, values, control flow, checks) operates.

### 3.3 Recovery Object Taxonomy

A **recovery object** is any semantic unit that P-CIL attempts to reconstruct. Every recovery object MUST be in exactly one of three states:

**3.3.1 Resolved**

A recovery object is *resolved* when:
- Evidence is sufficient (confidence >= 0.80, i.e., Strong or Definitive level)
- No unresolved ambiguity remains
- All required fields are populated
- The object is ready for direct lowering to CIL

A resolved object carries its evidence anchors and confidence score, but requires no special consumer handling beyond normal lowering.

**3.3.2 Partial**

A recovery object is *partial* when:
- Some evidence has been collected but is incomplete
- Certain fields are populated while others remain unknown
- Confidence is Moderate (0.50 -- 0.79) OR some required subcomponents are missing

A partial object MUST carry:
- `known`: the fields and relationships that have been recovered
- `missing`: an explicit enumeration of what remains unrecovered
- `confidence`: the aggregate confidence of the known portion
- `evidence_anchors`: anchors for the known portion

Partial objects are valuable: they represent genuine progress toward recovery and MUST NOT be discarded in favor of fully-unresolved forms when partial information exists.

[Evidence: LESSONS_LEARNED:lesson-8-recognized-but-fallback-is-valuable]

**3.3.3 Unresolved**

A recovery object is *unresolved* when:
- Evidence is Weak (< 0.50) or Insufficient (< 0.20)
- OR the domain cannot be determined at all

An unresolved object MUST use the canonical form:

```
Unresolved<Domain>(reason, evidence_so_far)
```

Where:
- `Domain` identifies the semantic area (e.g., `Call`, `Value`, `Check`, `Carrier`)
- `reason` explains why resolution failed (mandatory)
- `evidence_so_far` preserves any partial observations (may be empty)

Consumers MUST handle unresolved objects without crashing. Lowering passes MUST either emit a conservative fallback or propagate the unresolved marker downstream.

### 3.4 Evidence Model

Every recovery claim MUST be grounded in evidence. Evidence is not optional metadata; it is a structural requirement of P-CIL.

**3.4.1 Evidence Source Categories**

| Source | Description | Typical Confidence Contribution |
|--------|------------|-------------------------------|
| `symbol` | Named symbol from debug info, linking context, or IL2CPPAnalyzer enrichment | High (0.85 -- 1.0) |
| `metadata` | IL2CPP metadata blob: type info, method info, field info, string literals | High (0.85 -- 1.0) |
| `pattern` | Code pattern match: instruction sequence, control-flow template, data-flow shape | Medium (0.50 -- 0.85) |
| `hybrid` | Combination of two or more source categories corroborating each other | Varies; corroboration raises confidence |
| `manual` | User annotation or ground-truth label | Definitive (1.0) when trusted |

**3.4.2 Evidence Anchors**

An evidence anchor is a traceable link from a recovery claim to its grounding. Four anchor types exist:

| Anchor Type | Description | Example |
|------------|-------------|---------|
| **symbol anchor** | Named function, variable, or type from symbol table | `il2cpp_codegen_get_virtual_invoke_data` |
| **metadata anchor** | Offset, token, or index into IL2CPP metadata | `MethodInfo* at metadata offset 0x1A340` |
| **pattern anchor** | Instruction sequence or code slice that was matched | `CALL followed by STORE to params[] array` |
| **control-template anchor** | Control-flow structure fingerprint | `if-init-then-publish atomic pattern` |

Every normative recovery rule MUST specify which anchor types constitute sufficient evidence for that rule.

**3.4.3 Evidence Composition**

When multiple evidence sources exist for a single recovery object:

1. **Corroboration**: independent sources agreeing raises confidence. Two Moderate sources with consistent conclusions MAY yield Strong confidence.
2. **Contradiction**: conflicting sources MUST trigger ambiguity (see 3.6). The recovery object MUST NOT silently choose one interpretation.
3. **Subsumption**: a higher-quality source MAY subsume a lower-quality one if the higher source strictly contains the information of the lower. The subsumed source is retained for traceability but does not independently contribute to confidence.

[Evidence: LESSONS_LEARNED:lesson-12-fact-preservation-beats-heuristic-sophistication]

### 3.5 Confidence Model

Confidence is a numeric value in [0.0, 1.0] representing how strongly evidence supports a recovery claim. Confidence is NOT a probability; it is an evidence sufficiency measure.

**3.5.1 Calibration Rules**

Confidence MUST be computed from evidence, not assigned arbitrarily. The following calibration guidelines apply:

| Evidence Combination | Confidence Range | Level |
|---------------------|-----------------|-------|
| Symbol match + metadata confirmation | 0.95 -- 1.0 | Definitive |
| Symbol match alone | 0.85 -- 0.94 | Strong |
| Metadata match alone | 0.85 -- 0.94 | Strong |
| Pattern match + metadata partial support | 0.75 -- 0.89 | Strong to Moderate |
| Pattern match alone | 0.50 -- 0.75 | Moderate |
| Heuristic or positional inference | 0.20 -- 0.49 | Weak |
| No meaningful evidence | 0.00 -- 0.19 | Insufficient |

**3.5.2 Confidence Thresholds**

| Threshold | Meaning | Lowering Permission |
|-----------|---------|-------------------|
| >= 0.80 | May lower as resolved | Direct CIL emission permitted |
| 0.50 -- 0.79 | May lower conservatively | Conservative emission with diagnostic annotation |
| 0.20 -- 0.49 | Recognized-but-fallback | Protocol family identified; MUST NOT lower as resolved; SHOULD emit fallback with diagnostic |
| < 0.20 | Unresolved | MUST NOT lower as resolved; MUST emit unresolved marker or opaque fallback |

**3.5.3 Confidence Propagation**

When a recovery object depends on sub-recoveries (e.g., a call recovery depends on carrier recovery and target recovery), the composite confidence MUST NOT exceed the minimum confidence of its required sub-recoveries. Optional sub-recoveries do not constrain the composite at creation time. However, if an optional sub-recovery later transitions to unresolved (e.g., evidence is invalidated), the composite confidence SHOULD be re-evaluated; a previously-resolved composite MAY be downgraded to partial if the optional sub-recovery's absence materially affects semantic completeness.

[Evidence: LESSONS_LEARNED:lesson-2-conservative-failure-better-than-wrong-recovery]

### 3.6 Ambiguity

Ambiguity arises when evidence supports multiple equally plausible interpretations of the same host substrate observation.

**3.6.1 Representation**

An ambiguous recovery object carries:
- `ambiguous: true`
- `candidates`: list of candidate interpretations, each with its own confidence and evidence
- `preferred`: optionally, the candidate that additional heuristics favor (but this does NOT resolve the ambiguity)

**3.6.2 Rules**

1. A recovery object MUST be marked ambiguous when two or more candidates have confidence within 0.15 of each other and no additional evidence can distinguish them.
2. Ambiguous objects MUST NOT be lowered as if a single interpretation were certain.
3. Lowering passes SHOULD select the conservative union of candidates' effects, or emit a diagnostic fallback.
4. Ambiguity MUST be preserved through the pipeline; consumers downstream MAY resolve it with additional context.

### 3.7 Recognized-But-Fallback

The `recognized-but-fallback` state is a first-class recovery outcome, not a failure to hide.

[Evidence: LESSONS_LEARNED:lesson-8-recognized-but-fallback-is-valuable]

A recovery object is in recognized-but-fallback state when:
- The protocol family has been identified (e.g., "this is an invoker call")
- But evidence is insufficient for full resolution (e.g., the exact callee cannot be determined)
- Confidence falls in the Weak range (0.20 -- 0.49)

This state MUST be:
1. **Observable**: exposed in diagnostics, reports, and serialized output
2. **Actionable**: provides useful information for downstream analysis even without full resolution
3. **Stable**: does not spontaneously promote to resolved without new evidence

The recognized-but-fallback state is valuable for:
- Measuring recovery progress (how many calls are at least family-classified)
- Guiding manual analysis (analyst knows which family to investigate)
- Enabling incremental improvement (new evidence can promote to partial or resolved)

### 3.8 State Machines

P-CIL models four stateful domains using explicit state machines. These state machines track IL2CPP runtime state that cannot be recovered as a single atomic observation but requires tracking transitions across a function body.

[Source: il2cpp/libil2cpp/vm/Runtime.cpp:ClassInit]

**3.8.1 ClassInitState**

Tracks whether a type's static constructor has been executed.

```
States: Unknown | NotStarted | Running | Done | Failed

Transitions:
  Unknown     -> NotStarted     [first reference to type observed]
  NotStarted  -> Running        [class init call detected]
  Running     -> Done           [init completed successfully]
  Running     -> Failed         [init threw exception; exception cached]
  Failed      -> Failed         [subsequent access re-raises cached exception]
```

IL2CPP caches initialization failures and re-throws the same exception on subsequent access. Recovery MUST model this: a Failed type does not re-attempt initialization.

[Source: il2cpp/libil2cpp/vm/Runtime.cpp:ClassInit-reentrant-detection]

**3.8.2 MetaSlotState**

Tracks the lifecycle of a runtime metadata slot.

```
States: Unknown | EncodedToken | InitializedPtr | InvalidToken | Null

Transitions:
  Unknown       -> EncodedToken    [slot read reveals low-bit=1 encoding]
  EncodedToken  -> InitializedPtr  [atomic init publishes aligned pointer]
  EncodedToken  -> InvalidToken    [encoded token is malformed]
  EncodedToken  -> Null            [slot resolves to null]
  InitializedPtr -> InitializedPtr [stable; repeated reads are safe]
```

The EncodedToken -> InitializedPtr transition uses atomic read + atomic publish semantics. Recovery MUST NOT assume a slot is initialized without evidence of the init sequence.

[Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:metadata-slot-init]

**3.8.3 RgctxState**

Tracks runtime generic context initialization.

```
States: Unknown | Uninitialized | Initialized | Failed

Transitions:
  Unknown       -> Uninitialized  [RGCTX block referenced but not yet initialized]
  Uninitialized -> Initialized    [init call completed]
  Uninitialized -> Failed         [init failed]
  Initialized   -> Initialized    [stable; reads are safe]
```

Two access policies exist:
- `init`: triggers initialization if not yet done (via `il2cpp_rgctx_data`)
- `no_init`: reads without triggering initialization (via `il2cpp_rgctx_data_no_init`)

Recovery MUST distinguish these policies. A `no_init` access on an Uninitialized RGCTX does not cause a state transition.

[Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:rgctx-data-access]

**3.8.4 ExceptionState**

Tracks exception flow within a function.

```
States: None | Pending | Escaped

Transitions:
  None    -> Pending  [exception raised within function]
  Pending -> Escaped  [exception left function or was caught]
  Pending -> None     [exception caught and handled within function]
```

ExceptionState is function-scoped. It interacts with EH regions (Chapter 6) but is tracked independently of native C++ try/catch visibility.

### 3.9 Recovery Lifecycle

Recovery proceeds through a defined lifecycle for each recovery object:

```
Observation -> Evidence Collection -> Classification -> Resolution | Fallback
```

**3.9.1 Observation**

The host substrate presents a raw fact (P-Code operation, HighFunction call site, varnode, symbol reference). This is the input to recovery.

**3.9.2 Evidence Collection**

Evidence is gathered from available sources:
1. Host substrate facts (P-Code operands, def-use chains, call targets)
2. IL2CPPAnalyzer enrichment (metadata symbols, ABI flags, type layouts)
3. Pattern matching (instruction sequences, control-flow templates)
4. Cross-reference with other recovery objects in the same function

Evidence collection MUST be exhaustive within available sources before classification proceeds. Premature classification on insufficient evidence is a specification violation.

[Evidence: LESSONS_LEARNED:lesson-4-positional-heuristics-signal-weak-substrate]

**3.9.3 Classification**

Based on collected evidence, the recovery object is assigned to a domain (call, value, check, control-flow) and a protocol family within that domain. Classification produces a confidence score.

**3.9.4 Resolution Or Fallback**

Based on confidence:
- Confidence >= 0.80: proceed to resolution; populate all required fields
- Confidence 0.50 -- 0.79: create partial recovery object
- Confidence 0.20 -- 0.49: enter recognized-but-fallback state
- Confidence < 0.20: create unresolved object

### 3.10 Fallback Eligibility

Not all fallback strategies are always available. Fallback eligibility depends on the domain:

| Fallback Strategy | Description | When Eligible |
|------------------|-------------|---------------|
| `unknown_effect` | Assume the operation may have arbitrary side effects | Always eligible; most conservative |
| `base_only` | Retain original host substrate operation; drop recovery overlay | Always eligible when base op exists |
| `conservative_throw` | Assume the operation may throw any exception | Eligible for operations that could plausibly throw |

**Rules**:
1. A fallback MUST preserve the original host substrate base operation. Fallback MUST NOT delete base ops.
2. A fallback MUST NOT promote confidence: if evidence is Weak, the fallback representation MUST carry Weak confidence.
3. A fallback SHOULD carry the `recognized-but-fallback` marker when the protocol family is known but resolution failed.
4. Consumers MUST NOT fail (crash, abort, or refuse to load) when encountering a fallback.

[Evidence: LESSONS_LEARNED:lesson-2-conservative-failure-better-than-wrong-recovery]

### 3.11 Source Anchors

| Anchor | Used In |
|--------|---------|
| [Source: il2cpp/libil2cpp/vm/Runtime.cpp:ClassInit] | 3.8.1 ClassInitState |
| [Source: il2cpp/libil2cpp/vm/Runtime.cpp:ClassInit-reentrant-detection] | 3.8.1 reentrant init |
| [Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:metadata-slot-init] | 3.8.2 MetaSlotState |
| [Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:rgctx-data-access] | 3.8.3 RgctxState |
| [Evidence: LESSONS_LEARNED:lesson-2-conservative-failure-better-than-wrong-recovery] | 3.5.2, 3.10 |
| [Evidence: LESSONS_LEARNED:lesson-4-positional-heuristics-signal-weak-substrate] | 3.9.2 |
| [Evidence: LESSONS_LEARNED:lesson-8-recognized-but-fallback-is-valuable] | 3.3.2, 3.7 |
| [Evidence: LESSONS_LEARNED:lesson-12-fact-preservation-beats-heuristic-sophistication] | 3.4.3 |

---

## Chapter 4: Value And Data Model

### 4.1 Purpose

This chapter defines how P-CIL models values and data objects: value categories, type-transforming operations (box, unbox, isinst, castclass), array operations, field access (instance, static, thread-static), string literal recovery, and delegate construction.

### 4.2 Semantic Target

| Category | CIL Instructions |
|---|---|
| Type transformation | `box`, `unbox`, `unbox.any`, `isinst`, `castclass` |
| Array operations | `newarr`, `ldlen`, `ldelem.*`, `stelem.*`, `ldelema` |
| Field access (instance) | `ldfld`, `ldflda`, `stfld` |
| Field access (static) | `ldsfld`, `ldsflda`, `stsfld` |
| String literal | `ldstr` |
| Delegate construction | `newobj <DelegateType>::.ctor(object, IntPtr)` |

> Note: Thread-static field access maps to the same CIL opcodes but has a distinct IL2CPP codegen path. P-CIL models this explicitly.

### 4.3 Value Categories

| Category | Description | CIL Counterpart |
|---|---|---|
| `scalar` | Primitive numeric or boolean | CIL evaluation stack scalar |
| `native_ptr` | Unmanaged pointer | `native int` as pointer |
| `managed_ref` | Managed reference (byref) | `&` type |
| `managed_valaddr` | Address of value-type instance | Pointer into managed heap/stack |
| `rgctx_item` | RGCTX-sourced metadata reference | Elided at lowering |
| `meta_slot_ref` | Reference to metadata global slot | Elided at lowering |
| `unknown` | Cannot determine from evidence | Triggers partial/unresolved |

### 4.4 Recovery Rules

#### 4.4.1 Box

`Box(TypeInfoFor(T), &value)`. Nullable/variable-sized types use specialized branch patterns.

[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:Code.Box]

#### 4.4.2 Unbox / Unbox.Any

`UnBox(obj)` or `UnBox(obj, expectedBoxedClass)`. Includes NullCheck (Ch 7). `unbox` produces `managed_valaddr`; `unbox.any` produces value copy.

#### 4.4.3 IsInst

Three variants: `IsInst()` (general), `IsInstSealed()` (sealed type), `IsInstClass()` (non-interface class). Returns obj or NULL.

#### 4.4.4 Castclass

Three variants mirroring IsInst: `Castclass()`, `CastclassSealed()`, `CastclassClass()`. Throws `InvalidCastException` on failure.

#### 4.4.5 Array Operations

- **Ldlen**: `((RuntimeArray*)array)->max_length`
- **Ldelem**: Null check + bounds check + element access
- **Stelem**: Null check + bounds check + optional `ArrayElementTypeCheck` + store
- **Newarr**: `il2cpp_codegen_new_sz_array(type, length)`

[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:Code.Ldlen]

#### 4.4.6 Instance Field Access

Direct struct member access: `obj->fieldName`. Variable-sized types use `il2cpp_codegen_read/write_instance_field_data` with `RuntimeField*`.

#### 4.4.7 Static Field Access

Accessed through `il2cpp_codegen_static_fields_for(TypeInfo)`. Class init emitted before access.

[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:field-access-emission]

#### 4.4.8 Thread-Static Field Access (IL2CPP Overlay)

Distinct codegen path: `il2cpp_codegen_get_thread_static_data(TypeInfo)`. Recovered object MUST be annotated `thread_static: true`. Tiny backend does NOT support thread-static fields.

#### 4.4.9 String Literal Recovery

Loaded from metadata global slot with usage type `StringLiteral` (Annex C.6.3, value 5).

#### 4.4.10 Delegate Construction

Delegate `.ctor` writes carrier fields per Annex C.7.2: `method_ptr`, `invoke_impl`, `method`, `m_target`, `method_code`, `method_is_virtual`, `extra_arg`. Construction is the *storage side*; invocation is in Chapter 5.4.8.

[Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-ctor]

### 4.5 Binding Keys

| Key Format | Description |
|---|---|
| `tok<type>` | Type metadata token |
| `tok<method>` | Method metadata token |
| `tok<field>` | Field metadata token |
| `tok<string>` | String literal token |
| `tok<metaUsage>` | Raw metadata usage entry |
| `addr<>` | Code or data address |
| `ptr<>` | Pointer-typed reference |

### 4.6 Recovery Forms

| Operation | Resolved | Partial | Unresolved |
|---|---|---|---|
| Box/Unbox | Type info traced | Helper matched, type opaque | Allocation-like call unconfirmed |
| IsInst/Castclass | Target type + variant identified | Helper matched, type unknown | Conditional null pattern unconfirmed |
| Array ops | Element type + array type known | Array access confirmed, element type unknown | Indexed memory access unconfirmed |
| Field access | Field token resolved, access type classified | Struct access confirmed, field unknown | Memory access at offset unconfirmed |
| String literal | Slot decoded to string content | Slot identified as StringLiteral, content unavailable | Global read, usage type unknown |
| Delegate ctor | Binding mode + target method determined | Field writes observed, stub unknown | Object construction, delegate unconfirmed |

### 4.7 Lowering Obligations

- **Resolved**: Lower to corresponding CIL instructions with correct type tokens. Strip IL2CPP helpers.
- **Thread-static**: Lower to `ldsfld`/`stsfld` with `[ThreadStatic]` field. Elide accessor calls.
- **String literals**: Lower to `ldstr <token>`. Elide metadata scaffolding.
- **Delegate construction**: Lower to `newobj <DelegateType>::.ctor(object, IntPtr)`. Field-level setup is elided.

### 4.8 Source Anchors

| Anchor | Used In |
|---|---|
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:Code.Box] | 4.4.1 Box |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:UnBox] | 4.4.2 Unbox / Unbox.Any |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:IsInst] | 4.4.3 IsInst (IsInst, IsInstSealed, IsInstClass) |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:Castclass] | 4.4.4 Castclass (Castclass, CastclassSealed, CastclassClass) |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:Code.Ldlen] | 4.4.5 Array operations |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:field-access-emission] | 4.4.6, 4.4.7 Instance and static field access |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:thread-static-access] | 4.4.8 Thread-static field access |
| [Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:StringLiteral] | 4.4.9 String literal recovery (metadata global usage type 5) |
| [Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-ctor] | 4.4.10 Delegate construction |

---

## Chapter 5: Call Semantics

### 5.1 Purpose

This chapter defines how P-CIL recovers CIL call-site semantics from native call observations in IL2CPP-compiled binaries. Every native call site corresponds to one (or, in the delegate multicast case, a sequence of) CIL call instructions. The recovery problem is: given an observed P-Code `CALL` or `CALLIND` operation with its parameter list, determine which CIL instruction it represents, what method it targets, and what carriers (Annex C) participate in the dispatch.

IL2CPP transforms CIL call instructions through a non-trivial expansion: hidden parameters are appended, virtual dispatch is lowered to struct-based lookup, delegate invocation becomes stub-mediated indirect call, and generic sharing may route calls through invoker trampolines. Call semantics recovery reverses these transformations by classifying each call site into a **protocol family** and extracting the CIL-level method reference.

### 5.2 Semantic Target

The CIL call instructions this chapter recovers:

| CIL Instruction | Description | Recovery Status in v1 |
|---|---|---|
| `call` | Direct call to statically-known method | Fully specified (5.4.1, 5.4.2) |
| `callvirt` | Virtual dispatch (may resolve to direct if sealed/final) | Fully specified (5.4.3 -- 5.4.8) |
| `calli` | Indirect call through function pointer | Covered under Unresolved (5.4.9) when target is opaque |
| `newobj` | Object allocation + constructor call | Recovered as DirectManaged call to `.ctor` (5.4.1); allocation-side recovery deferred to Chapter 4 |
| `ldftn` | Load method pointer | Deferred; method pointer loads are not call sites and require value-flow recovery outside Chapter 5 |
| `ldvirtftn` | Load virtual method pointer | Deferred; same rationale as `ldftn` |

> Note: The brief descriptions above are recovery target anchors, not ECMA-335 semantic restatements. See ECMA-335 III.3/III.4 for authoritative definitions. `newobj` is recovered through the call protocol families defined in this chapter (the `.ctor` call). `ldftn` and `ldvirtftn` produce method pointers that are consumed later (typically by delegate construction in Chapter 4); their recovery rules will be specified in a future revision.

### 5.3 Evidence Sources

| Source | Kind | Contribution |
|---|---|---|
| P-Code `CALL` / `CALLIND` operations | Substrate | Call target address; parameter count and types |
| Ghidra HighFunction call sites | Substrate | Decompiled parameter lists with type propagation |
| IL2CPPAnalyzer symbol enrichment | Enrichment | Function names mapped to IL2CPP method metadata |
| Carrier traces (Annex C) | Hybrid | MethodInfo (C.3), VirtualInvokeData (C.5), delegate fields (C.7), RGCTX (C.4) |
| Calling convention analysis | Pattern | Parameter count and layout vs. expected IL2CPP signatures |
| Metadata registration tables | Metadata | Address-to-method mapping |

### 5.4 Recovery Rules -- Protocol Family Taxonomy

Each native call site MUST be classified into exactly one **protocol family**. The families are grounded in IL2CPP's `MethodCallType` and virtual dispatch sub-classification.

[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EmitCallExpression]

#### 5.4.1 DirectManaged

Non-virtual call to a statically-known managed method using the standard function pointer cast.

**Precondition**: `MethodCallType == Normal` AND `DoCallViaInvoker() == false`.

**Evidence**: Call target is a statically-resolved function pointer; callee address matches known method via symbol or metadata; parameter count matches `this? + explicit_params + hiddenMethodInfo?`.

**Carriers**: MethodInfo (Annex C.3) as final parameter per C.3.2 rules. RGCTX (C.4) if callee is in shared generic code.

**Native signature**: `((FunctionPointerType)methodPtr)(this?, p1, ..., pN, hiddenMethodInfo?)`

**Post-state**: Resolved -> `call <method>` or `callvirt <method>` (if devirtualized).

[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:DirectCallFor]

#### 5.4.2 Invoker

Direct call routed through the invoker trampoline when `DoCallViaInvoker()` returns true (full generic sharing).

**Precondition**: `MethodCallType == Normal` AND `DoCallViaInvoker() == true`.

**Evidence**: Call target matches invoker stub with 5-parameter signature; static methods pass `NULL` as obj.

**Carriers**: MethodInfo (C.3) passed as `method` parameter (2nd argument).

**Native signature**:
```c
invoker_method(Il2CppMethodPointer methodPtr,
               const RuntimeMethod* method,
               void* obj,
               void** params,
               void* retVal)
```

Parameter marshaling: non-pointer args by address in `params[]`; pointer args by value. Return via `retVal`; void passes `NULL`.

**Post-state**: Resolved -> `call <method>` with arguments unmarshaled from invoker protocol.

[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:InvokerCallFor]

#### 5.4.3 Virtual

Non-generic virtual method dispatch through vtable slot lookup.

**Precondition**: `VirtualMethodCallType == Virtual`.

**Evidence**: Call to `il2cpp_codegen_get_virtual_invoke_data(slot, obj)` or vtable slot access; `slot` is compile-time constant.

**Carriers**: VirtualInvokeData (C.5) with `methodPtr` and `method` fields. MethodInfo carried within `VirtualInvokeData.method`.

**Direct path**: `((FunctionPointerType)invokeData.methodPtr)(obj, p1, ..., pN, invokeData.method)`

**Invoker path**: `invokeData.method->invoker_method(methodPtr, method, obj, params[], retVal)`

**Post-state**: Resolved -> `callvirt <method>`.

[Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:WriteVirtual]

#### 5.4.4 GenericVirtual

Virtual dispatch of a generic method instance requiring runtime inflation.

**Precondition**: `VirtualMethodCallType == GenericVirtual`.

**Evidence**: Call to `il2cpp_codegen_get_generic_virtual_invoke_data(method, obj, &invokeData)`; `method` is generic method instance metadata.

**Carriers**: VirtualInvokeData (C.5) written to caller-local struct. MethodInfo (C.3) in creation function's `method` parameter.

**Post-state**: Resolved -> `callvirt <generic_method_instance>`.

[Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:WriteGenericVirtual]

#### 5.4.5 Interface

Non-generic interface method dispatch with interface type carrier.

**Precondition**: `VirtualMethodCallType == Interface`.

**Evidence**: Call to `il2cpp_codegen_get_interface_invoke_data(slot, obj, declaringInterface)`; additional `RuntimeClass*` operand for the interface type.

**Carriers**: VirtualInvokeData (C.5). TypeInfo for declaring interface type.

**Post-state**: Resolved -> `callvirt <interface_method>`.

[Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:WriteInterface]

#### 5.4.6 GenericInterface

Interface dispatch of a generic method instance.

**Precondition**: `VirtualMethodCallType == GenericInterface`.

**Evidence**: Call to `il2cpp_codegen_get_generic_interface_invoke_data(method, obj, &invokeData)`.

**Carriers**: VirtualInvokeData (C.5). MethodInfo (C.3) for generic interface method instance.

**Post-state**: Resolved -> `callvirt <generic_interface_method_instance>`.

[Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:WriteGenericInterface]

#### 5.4.7 Constrained

`constrained.` prefix call on a type that may be value or reference, requiring conditional dispatch.

**Precondition**: `_constrainedCallThisType` is non-null in codegen context.

**Evidence**: Presence of `Il2CppFakeBox`, `il2cpp_codegen_runtime_constrained_call`, or conditional boxing before virtual dispatch.

**Resolution paths**:
- Value type, method found: resolves to DirectManaged (5.4.1); `this` passed by reference without boxing.
- Value type, inherited method: box + Virtual (5.4.3) or Interface (5.4.5).
- Reference type: dereference `this` + Virtual dispatch.
- Shared generic, variable-sized type: `ConstrainedInvokerCall` through runtime helper.

**Carriers**: TypeInfo for constrained type. MethodInfo for constrained method. May further reference VirtualInvokeData (C.5).

**Post-state**: Resolved -> `constrained. <T> callvirt <method>`.

[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:WriteConstrainedCallExpressionFor]

#### 5.4.8 DelegateInvoke

Delegate invocation through the `Invoke` method, mediated by the delegate's `invoke_impl` stub. This is an **overlay** in call semantics; delegate *construction* is in Chapter 4.

**Precondition**: Call target is delegate's `Invoke`, OR indirect call through `invoke_impl` field.

**Evidence**: Call reads `invoke_impl`, `method_code`, `method` from delegate object. Multicast: loop over `delegates` array.

**Carriers**: Delegate carriers (C.7): `invoke_impl`, `method_code`, `method`, `m_target`.

**Single delegate**:
```c
((FunctionPointerType)__this->invoke_impl)(
    (Il2CppObject*)__this->method_code, p1, ..., pN,
    (RuntimeMethod*)__this->method);
```

**Multicast**: Loop over `delegates` array, invoke each.

**Post-state**: Resolved -> `callvirt <DelegateType>::Invoke(...)`.

[Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-invoke]

#### 5.4.9 Unresolved

Call target cannot be classified into any of the above families.

**Representation**: `Unresolved<Call>(reason, evidence_so_far)`

Permitted reasons: `"opaque indirect call"`, `"parameter count ambiguous"`, `"no metadata for target address"`, `"carrier conflict"`, `"stripped method body"`.

`evidence_so_far` MUST preserve: target address, observed parameter count, carrier fragments, partial symbol matches.

### 5.5 Recovery Forms

| Family | Resolved | Partial | Unresolved |
|---|---|---|---|
| **DirectManaged** | Callee + MethodInfo presence/value confirmed | Callee address known but not in metadata; OR MethodInfo uncertain | Cannot determine if direct managed |
| **Invoker** | Target method from MethodInfo + params unmarshaled | Invoker recognized but MethodInfo value not traced | Invoker-like signature but unconfirmed |
| **Virtual** | Slot + object type + method resolved | VirtualInvokeData matched but slot is runtime variable | Indirect call through struct but origin unconfirmed |
| **GenericVirtual** | Generic method instance + full dispatch chain | Lookup matched but method metadata unresolved | Indistinguishable from non-generic virtual |
| **Interface** | Slot + declaring interface + method resolved | Interface lookup matched but interface type unknown | Indistinguishable from regular virtual |
| **GenericInterface** | Generic interface method instance resolved | Lookup matched but metadata unresolved | Indistinguishable from other generic dispatch |
| **Constrained** | Constrained type + resolution path determined | Pattern detected but resolution path ambiguous | FakeBox observed but semantics unconfirmed |
| **DelegateInvoke** | Delegate type + binding mode + target method | Pattern matched but stub type or target unknown | Indirect call through object field but delegate unconfirmed |

### 5.6 Confidence And Ambiguity

**Composite confidence**: Per Chapter 3.5.3: `confidence(Call) = min(confidence(target), confidence(carriers...))`.

**Ambiguity scenarios**:
1. **DirectManaged vs. Invoker**: parameter count 5 could match either. Resolution: check if param 4 is `void**` (invoker) or typed.
2. **Virtual vs. Interface**: declaring type uncertain. Resolution: trace `declaringInterface` parameter.
3. **DelegateInvoke vs. indirect call**: object type uncertain. Resolution: check delegate field layout.
4. **Constrained**: inherently ambiguous until constrained type resolved. SHOULD carry both interpretations.

When ambiguity cannot be resolved: mark `ambiguous: true` per Chapter 3.6.

### 5.7 Lowering Obligations

**Resolved calls**:

| Family | CIL Instruction |
|---|---|
| DirectManaged (instance/devirtualized) | `call` or `callvirt <method>` |
| DirectManaged (static) | `call <method>` |
| Invoker | Same as DirectManaged (invoker is transparent to CIL) |
| Virtual / GenericVirtual | `callvirt <method>` |
| Interface / GenericInterface | `callvirt <interface_method>` |
| Constrained (value type, direct) | `constrained. <T> callvirt <method>` |
| DelegateInvoke | `callvirt <DelegateType>::Invoke(...)` |

Lowering MUST strip: hidden MethodInfo params, VirtualInvokeData creation calls, invoker marshaling (`params[]`, `retVal`), delegate `invoke_impl` indirection.

**Partial calls**: Emit with diagnostic annotations. Missing carriers -> unresolved operands.

**Unresolved calls**: Emit effect-preserving fallback (opaque call stub or base operation passthrough with diagnostic). Calls always have potential side effects, so `nop` is NOT permitted for unresolved calls (per 10.4.4). MUST NOT introduce new control-flow beyond what the base operation implies.

**Recognized-but-fallback**: MUST NOT emit resolved CIL. Preserve family identification as diagnostic.

### 5.8 IL2CPP Overlay References

| Overlay | Affects | Annex C Reference |
|---|---|---|
| Hidden MethodInfo | All DirectManaged + devirtualized calls | C.3 |
| Invoker ABI | All families when `DoCallViaInvoker()` is true | C.5.3 |
| Virtual/Interface dispatch | All `callvirt` through vtable | C.5 |
| Delegate invoke protocol | All delegate calls | C.7 |
| Generic sharing adapters | Calls in shared generic bodies | C.4, C.3.4 |

### 5.9 Source Anchors

| Anchor | Region |
|---|---|
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EmitCallExpression] | Top-level call emission |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:DirectCallFor] | Direct call assembly |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:InvokerCallFor] | Invoker trampoline assembly |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:WriteConstrainedCallExpressionFor] | Constrained call |
| [Source: il2cpp/Unity.IL2CPP/MethodSignatureWriter.cs:NeedsHiddenMethodInfo] | Hidden MethodInfo decision |
| [Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:WriteVirtual] | Virtual dispatch |
| [Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:WriteGenericVirtual] | Generic virtual |
| [Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:WriteInterface] | Interface dispatch |
| [Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:WriteGenericInterface] | Generic interface |
| [Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:consumption-patterns] | Direct vs invoker consumption |
| [Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-invoke] | Delegate invocation |
| [Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-stub-selection] | Stub selection |

---

## Chapter 6: Control Flow And Exception Model

### 6.1 Purpose

This chapter defines how P-CIL recovers CIL-level control flow structures and exception handling (EH) clauses. IL2CPP transforms CIL's structured EH into C++ `try`/`catch` (Full runtime) or goto-chain emulations (Tiny runtime). Recovery reconstructs CIL EH regions from host substrate CFG and IL2CPP-specific exception patterns.

### 6.2 Semantic Target

| CIL Structure | Description |
|---|---|
| `.try` region | Protected region |
| `catch` handler | Typed exception handler |
| `filter` handler | User-code-driven exception filter |
| `finally` handler | Unconditional cleanup handler |
| `fault` handler | Exception-only cleanup handler |
| `throw` / `rethrow` | Exception raise / re-raise |
| `leave` | Exit protected region |

### 6.3 Evidence Sources

| Source | Contribution |
|---|---|
| P-Code CFG | Basic blocks, edges, dominators |
| C++ `try/catch(Il2CppExceptionWrapper&)` | EH region boundaries (Full runtime) |
| `il2cpp::utils::Finally` / `Fault` RAII | Finally/fault identification |
| `IL2CPP_RAISE_MANAGED_EXCEPTION` | Throw sites |
| `IL2CPP_PUSH/POP_ACTIVE_EXCEPTION` | Catch entry/exit |
| `il2cpp_codegen_class_is_assignable_from` | Catch type-check cascade |
| `IL2CPP_LEAVE` / `IL2CPP_JUMP_TBL` (Tiny) | Goto-chain EH |

### 6.4 Recovery Rules

#### 6.4.1 CFG From Host Substrate

Host substrate MUST supply basic blocks, edges, dominators. P-CIL does not define how these are computed.

#### 6.4.2 Full Runtime EH Recovery

**Try region**: C++ `try` block or `FinallyHelper` RAII scope.

**Catch handler**: Type-check cascade in `catch(Il2CppExceptionWrapper& e)` block. Each `if(il2cpp_codegen_class_is_assignable_from(...))` produces one CIL catch clause. Ordering preserves CIL clause ordering.

**Filter handler**: `__filter_local` boolean + implicit try/catch wrapping filter evaluation. Conditional branch on `__filter_local` determines acceptance.

**Finally handler**: Body of `FinallyHelper<Block, false>` lambda. Destructor guarantees execution on both normal and exceptional exit.

**Fault handler**: Body of `FinallyHelper<Block, true>` lambda. Runs only on exceptional exit.

[Source: il2cpp/libil2cpp/vm-utils/Finally.h:FinallyHelper]
[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EnterTry]

#### 6.4.3 Tiny Runtime EH Recovery

Goto-chain macros: `IL2CPP_LEAVE`, `IL2CPP_END_FINALLY`, `IL2CPP_CLEANUP`, `IL2CPP_JUMP_TBL`. Recovery MUST recognize these as structured EH. Confidence is typically Moderate (0.50--0.75).

[Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-tiny.h:exception-macros]

#### 6.4.4 Throw And Rethrow

**Throw**: `il2cpp_codegen_raise_exception(ex, lastManagedFrame)` -> CIL `throw`.

**Rethrow**: `il2cpp_codegen_rethrow_exception(ex)` -> CIL `rethrow`. In finally blocks, via `StoreException` pattern.

[Source: il2cpp/libil2cpp/vm/Exception.cpp:Raise]

#### 6.4.5 ExceptionState Integration

Per Chapter 3.8.4: throw -> `None -> Pending`; catch accepts -> `Pending -> None`; no match -> `Pending -> Escaped`. ExceptionState is tracked independently of C++ try/catch visibility.

#### 6.4.6 Conservative Exception Edges

Exception edges MUST be produced even when handler cannot be precisely bound. Use conservative sink node for unresolvable throw sites.

### 6.5 Recovery Forms

| Structure | Resolved | Partial | Unresolved |
|---|---|---|---|
| try region | Boundaries + all handlers identified | Entry known, exit ambiguous | Control structure unconfirmed as EH |
| catch | Type + entry + exit determined | Cascade matched, type unresolved | Dispatch observed, pattern unconfirmed |
| finally/fault | Lambda body + RAII scope determined | Helper detected, boundaries unclear | Scope-exit pattern, Finally/Fault unclear |
| throw | Raise function + exception operand identified | Known raise, operand untraceable | NORETURN call, managed raise unconfirmed |

### 6.6 Lowering Obligations

**Resolved**: Emit CIL EH clause table entries. Elide all IL2CPP scaffolding (`Il2CppExceptionWrapper`, `ExceptionSupportStack`, `FinallyHelper`, `__filter_local`, active exception macros).

**Partial**: Best-effort EH clauses with diagnostic. Unresolved catch types use `System.Object` conservatively.

**Unresolved**: No EH clauses emitted. Conservative exception edges preserved in CFG.

### 6.7 Source Anchors

| Anchor | Used In |
|---|---|
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EnterTry] | 6.4.2 Try region emission (C++ try, FinallyHelper construction) |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:ExitTry] | 6.4.2.2 Catch dispatch: type-check cascade, exception push, StoreException |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EnterFilter] | 6.4.2.3 Filter handler: `__filter_local`, implicit try/catch |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:ExitFilter] | 6.4.2.3 Filter exit: conditional branch on `__filter_local` |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EnterFinally] | 6.4.2.4 Finally handler codegen |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EnterFault] | 6.4.2.5 Fault handler codegen |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:Code.Throw] | 6.4.4.1 Throw emission |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:Code.Rethrow] | 6.4.4.2 Rethrow emission |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EmitCodeForLeaveFromTry] | 6.4.5 Leave from try |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:EmitCodeForLeaveFromCatch] | 6.4.5 Leave from catch |
| [Source: il2cpp/Unity.IL2CPP/ExceptionSupport.cs:Prepare] | 6.4.6 Active exception stack declaration |
| [Source: il2cpp/libil2cpp/vm/Exception.cpp:Raise] | 6.4.4.1 Exception::Raise (PrepareExceptionForThrow + throw wrapper) |
| [Source: il2cpp/libil2cpp/vm/Exception.cpp:Rethrow] | 6.4.4.2 Exception::Rethrow (direct re-throw) |
| [Source: il2cpp/libil2cpp/vm-utils/Finally.h:FinallyHelper] | 6.4.2.4, 6.4.2.5 RAII finally/fault: destructor semantics |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:exception-macros] | 6.4.6 PUSH/POP/GET_ACTIVE_EXCEPTION, RAISE/RETHROW macros |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-tiny.h:exception-macros] | 6.4.3 Tiny: IL2CPP_LEAVE, IL2CPP_END_FINALLY, IL2CPP_JUMP_TBL |
| [Source: il2cpp/libil2cpp/utils/ExceptionSupportStack.h:ExceptionSupportStack] | 6.4.6 Active exception stack template |

---

## Chapter 7: Checks Model

### 7.1 Purpose

This chapter defines how P-CIL models runtime checks: operations that exist solely as guards or exception triggers. Checks do NOT produce values. This distinguishes them from type-transformation operations (box, unbox, isinst, castclass) in Chapter 4, which produce values even though they may also throw.

### 7.2 Semantic Target

| Check | CIL Semantic | IL2CPP Surface |
|---|---|---|
| Null check | NullReferenceException | `NullCheck(this_ptr)` |
| Bounds check | IndexOutOfRangeException | `IL2CPP_ARRAY_BOUNDS_CHECK(index, length)` |
| Array-store check | ArrayTypeMismatchException | `ArrayElementTypeCheck(array, value)` |
| Divide-by-zero | DivideByZeroException | `DivideByZeroCheck(denominator)` |
| Overflow check | OverflowException | `il2cpp_codegen_check_*_overflow` |

### 7.3 Recovery Rules

#### 7.3.1 Null Check

`NullCheck(ptr)` tests `ptr != NULL`, raises `NullReferenceException` on failure. Inserted before instance method calls, field access, array operations.

[Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:null-check]

#### 7.3.2 Bounds Check

`IL2CPP_ARRAY_BOUNDS_CHECK(index, length)` performs unsigned comparison. **Full runtime**: always emitted. **Tiny runtime**: debug-only (release expands to nothing).

[Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-tiny.h:bounds-check-debug-only]

#### 7.3.3 Array-Store Check

`ArrayElementTypeCheck(array, value)`. **Full runtime**: emitted for `stelem.ref`. **Tiny runtime**: no-op.

#### 7.3.4 Divide-By-Zero Check

`DivideByZeroCheck(denominator)` tests `den != 0`.

#### 7.3.5 Overflow Check

Helpers: `il2cpp_codegen_check_add_overflow`, `il2cpp_codegen_check_sub_overflow`, `il2cpp_codegen_check_mul_overflow_i64`. Emitted for CIL `*.ovf` instructions.

### 7.4 Profile Sensitivity

| Factor | Effect |
|---|---|
| Full runtime, debug | All checks present |
| Full runtime, release | Most checks present; some compiler-optimized away |
| Tiny runtime, debug | Bounds checks present; array-store absent |
| Tiny runtime, release | Bounds checks absent; array-store absent |

Absence of an expected check MUST NOT cause recovery failure. Annotate `check_expected_but_absent`.

### 7.5 Lowering Obligations

**Resolved checks**: SHOULD be elided when lowering to CIL, because the CLR inserts its own null checks, bounds checks, and type checks as part of the managed runtime contract. Exception: custom overflow check patterns that have no implicit CLR equivalent SHOULD be preserved as explicit `if` + `throw`.

**Partial checks**: The check's exception behavior is observable (it may throw). Lowering MUST preserve this exception-raising potential per Chapter 10 effect-preservation rules. Options:
- (a) Emit the check as an explicit guard (`if (condition) throw <ExceptionType>`) using whatever partial evidence is available (e.g., known check kind but unknown operand -> emit with conservative operand).
- (b) Rely on the CLR's implicit check for the same operation (e.g., a partial null check before `ldfld` is redundant because the CLR performs its own null check on field access). This is permitted ONLY when the subsequent CIL instruction provably triggers the same implicit check.

**Unresolved checks**: When a check-like pattern is observed but its semantic kind cannot be confirmed, lowering SHOULD retain the base operation unchanged (per `base_only` fallback strategy in Chapter 10.5). A `nop` is NOT permitted for unresolved checks because checks have observable exception behavior. Emit diagnostic annotation indicating the suspected but unconfirmed check.

**Check-absent cases**: When a check is expected but not observed (e.g., bounds check absent in Tiny release), no fallback is needed — the absence itself is the correct recovery. Annotate `check_expected_but_absent` for diagnostics; do not synthesize a check that was not present in the binary.

### 7.6 Boundary With Chapter 4

Chapter 4 owns value-producing operations (box, unbox, isinst, castclass). Chapter 7 owns pure guards (null, bounds, array-store, div-zero, overflow).

### 7.7 Source Anchors

| Anchor | Used In |
|---|---|
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:null-check] | 7.3.1 |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:IL2CPP_ARRAY_BOUNDS_CHECK] | 7.3.2 |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-tiny.h:bounds-check-debug-only] | 7.3.2, 7.4 |

---

## Chapter 8: Memory Ordering And Concurrency

### 8.1 Purpose

This chapter will define how P-CIL models memory ordering semantics and concurrency primitives: volatile loads/stores, memory barriers, interlocked operations, and monitor enter/exit.

### 8.2 Semantic Target

CIL instructions: `volatile.` prefix, `Interlocked.*`, `Monitor.Enter/Exit`, `Thread.MemoryBarrier`.

### 8.3 Intended Content (Outline)

- Volatile load/store recovery from `PCIL_VOLATILE_LOAD/STORE`
- Memory barrier recovery from `PCIL_MEMORY_BARRIER`
- Interlocked operations: exchange, compare_exchange, add, inc, dec
- Monitor enter/exit with exception-safety (try/finally pattern)
- GC write barriers (annotation, not CIL instruction)

> Reserved: This chapter ships as outline-only in v1. Full recovery rules, confidence guidance, and source anchors will be completed in a later revision. The concurrency domain requires careful grounding in both IL2CPP codegen patterns and Ghidra's memory-order analysis capabilities, which are not yet sufficiently characterized for normative specification.

---

## Chapter 9: Environment And Profiles

### 9.1 Purpose

This chapter will define the environment model that parameterizes P-CIL recovery: build profiles, backend profiles, and configuration axes that affect which IL2CPP codegen patterns appear.

### 9.2 Semantic Target

No single CIL instruction. The environment model affects all chapters: which checks are emitted (Ch 7), which call patterns appear (Ch 5), which metadata paths are used (Annex A).

### 9.3 Intended Content (Outline)

- **Backend profile**: `il2cpp_full` | `il2cpp_tiny` | `unknown`
- **Build profile**: `debug` | `release` | `unknown`
- **Check options**: `null_checks`, `bounds_checks`, `div0_checks`, `array_store_checks` (on/off/debug_only)
- **Generic sharing**: `none` | `ref_sharing` | `full_sharing` | `mixed` | `unknown`
- **Delegate via invokers**: `true` | `false` | `unknown`
- **Thread-static support**: `supported` | `unsupported` | `unknown`
- **EH model**: `native_try_catch` | `goto_chain` | `unknown`
- Guard conditions reference these environment dimensions

> Reserved: This chapter ships as outline-only in v1. The environment model will be formalized once the core recovery chapters (Ch 3, 5, 10) have been validated against real binaries across multiple profile configurations. Premature formalization risks encoding untested assumptions about profile interaction.

---

## Chapter 10: Lowering Contract

### 10.1 Purpose

This chapter defines the contract between P-CIL recovery objects and any downstream lowering pass that produces verifier-safe CIL bytecode. **Lowering** is the translation from P-CIL recovery objects to verifier-safe CIL. This chapter specifies the obligations that a conforming lowering pass MUST satisfy. It does NOT define a specific lowering implementation.

[Evidence: ISIL_RETIREMENT:downstream-lowering-role]

### 10.2 Semantic Target

The target of lowering is verifier-safe CIL bytecode satisfying three requirements:

1. **Verification safety.** Emitted bytecode MUST pass ECMA-335 verification rules.
2. **Semantic preservation.** Emitted bytecode MUST preserve the semantic intent of recovered P-CIL objects, to the degree permitted by recovery state and confidence.
3. **Diagnostic traceability.** Emitted output MUST carry diagnostic annotations for partial, recognized-but-fallback, or unresolved recovery objects.

### 10.3 Evidence Sources

| Source | What It Provides |
|---|---|
| Chapter 3 (Core Recovery Model) | Recovery states, confidence thresholds, fallback strategies, state machines |
| Chapter 5 (Call Semantics) | Call recovery objects with protocol family and carrier bindings |
| Annex A (Metadata Initialization) | Metadata init recovery objects |
| Annex C (Carrier Protocol Reference) | Carrier definitions and lifecycle |
| ISIL backend infrastructure | CFG, SSA, stackification, verifier-safe emission mechanics |

[Evidence: ISIL_RETIREMENT:backend-infrastructure-remains-valuable]

### 10.4 Lowering Obligations By Recovery State

#### 10.4.1 Resolved Objects (confidence >= 0.80)

1. MUST be lowerable to valid CIL instructions.
2. Lowered CIL MUST preserve semantic intent: correct opcode, operand types, stack behavior.
3. Evidence anchors SHOULD be propagated as diagnostic annotations.
4. Carrier bindings (Annex C) MUST be lowered faithfully; method pointer / metadata pointer distinction MUST NOT be collapsed.

[Evidence: LESSONS_LEARNED:lesson-12-fact-preservation-beats-heuristic-sophistication]

#### 10.4.2 Partial Objects (confidence 0.50 -- 0.79)

1. SHOULD be lowerable with conservative assumptions for missing components.
2. Missing carrier fields MUST be replaced with explicit placeholder operands, not silently dropped.
3. Lowered CIL MUST carry diagnostic annotation indicating which components were partial.
4. MAY emit runtime helper call instead of direct CIL when partial state makes direct emission unsafe. Helper MUST satisfy the shim justification rule (10.8).
5. Known fields MUST be preserved; MUST NOT discard known information because other fields are missing.

[Evidence: LESSONS_LEARNED:lesson-8-recognized-but-fallback-is-valuable]

#### 10.4.3 Recognized-But-Fallback Objects (confidence 0.20 -- 0.49)

1. MUST NOT be lowered as if resolved.
2. Protocol family identification MUST be preserved in diagnostic output.
3. SHOULD emit one of (in preference order):
   - (a) Runtime fallback helper preserving base operation's observable side effects (memory writes, exception possibilities, control-flow effects).
   - (b) Opaque intrinsic stub annotated with recognized family, whose effect signature conservatively covers the base operation's effects.
4. Original host substrate base operation MUST be preserved or its effects faithfully represented. A `nop` emission is permitted ONLY when the base operation is provably effect-free (no memory writes, no exceptions, no observable side effects). For operations with potential side effects (calls, stores, checks), `nop` violates this rule.

[Evidence: LESSONS_LEARNED:lesson-2-conservative-failure-better-than-wrong-recovery]

#### 10.4.4 Unresolved Objects (confidence < 0.20)

1. MUST produce well-defined fallback; MUST NOT produce invalid CIL, crash, or abort.
2. SHOULD emit an effect-preserving fallback: opaque intrinsic, runtime helper, or base operation passthrough. The fallback MUST conservatively represent the base operation's observable effects.
3. Unresolved reason and preserved evidence MUST be available in diagnostic output.
4. MUST NOT upgrade confidence level.
5. A `nop` emission is permitted ONLY when the base operation is provably effect-free. For unresolved calls, `nop` is NOT permitted because calls always have potential side effects; use an opaque call stub instead.

### 10.5 Guard And Fallback Model

#### 10.5.1 Guard Structure

| Component | Description | Example |
|---|---|---|
| **profile** | Runtime profile assumption | `full_runtime`, `tiny_runtime` |
| **options** | Boolean check options | `null_checks: on` |
| **assumptions** | Semantic assumptions | `generic_sharing: shared` |

Guards are defined by the recovery pass and are immutable at lowering time.

#### 10.5.2 Fallback Strategy Selection

| Strategy | Behavior | When To Use |
|---|---|---|
| `unknown_effect` | Assume arbitrary side effects; most conservative | Default; always eligible |
| `base_only` | Retain base substrate operation; drop overlay | When base op effects are well-defined |
| `conservative_throw` | Assume may throw any exception | When operation participates in exception flow |

Rules:
1. `unknown_effect` is the default when no other strategy applies.
2. `base_only` is preferred when base effects are fully known.
3. A lowering pass MUST NOT invent strategies weaker than the defined three.

### 10.6 Resilience Rules

These are the most critical normative requirements in this chapter:

**Rule 1: No crash on any recovery state.** A conforming lowering pass MUST NOT crash, abort, or refuse to process a function when encountering ANY recovery state, including unresolved objects with confidence 0.0.

**Rule 2: No silent deletion of base operations.** If an overlay cannot be lowered, the base operation's semantic effects MUST be preserved. Deletion permitted ONLY when the overlay's lowered form subsumes the base.

**Rule 3: No confidence promotion.** If recovery says Weak, lowering MUST NOT emit as if Definitive. Confidence is set by evidence; lowering has no authority to upgrade.

**Rule 4: Unknown overlay types are not errors.** Unknown overlays MUST be treated as `unknown_effect` fallback, not as errors.

**Rule 5: Total function coverage.** SHOULD produce output for every function in the input, even with mixed resolved and unresolved objects.

[Evidence: LESSONS_LEARNED:lesson-2-conservative-failure-better-than-wrong-recovery]

### 10.7 Lowering Fidelity Levels

Fidelity is a diagnostic classification, not a configuration knob:

| Level | Input Condition | Output |
|---|---|---|
| **Full** | confidence >= 0.80, all carriers resolved | Direct CIL emission; semantically faithful |
| **Conservative** | confidence 0.50 -- 0.79, some carriers partial | CIL with helpers/placeholders; gaps marked |
| **Diagnostic** | confidence 0.20 -- 0.49, family known | Fallback + diagnostic annotations |
| **Opaque** | confidence < 0.20, insufficient evidence | Intrinsic/nop + full diagnostic dump |

Rules:
1. Every lowered instruction MUST be classifiable into exactly one fidelity level.
2. A function's overall fidelity is the minimum of its instructions.
3. Fidelity MUST be reported in diagnostics.
4. Fidelity MUST NOT be used to filter or suppress output.

### 10.8 Runtime Shim Justification Rule

Every runtime helper emitted by lowering MUST satisfy:

> **"What IL2CPP runtime semantic does this helper preserve that cannot be expressed in pure CIL?"**

If the answer is "convenience" or "implementation simplicity", the shim is NOT justified.

Justified categories:

| Category | Justification |
|---|---|
| Metadata initialization | CIL has no atomic-publish-with-encoded-token primitive |
| RGCTX access | CIL has no runtime generic context array access |
| Invoker protocol | CIL has no invoker ABI |
| Class initialization | IL2CPP's cached-exception re-raise has no CIL equivalent |
| Virtual/interface dispatch | IL2CPP vtable layout differs from CIL virtual dispatch |

Rules:
1. Every shim MUST document its justification category.
2. Shims MUST NOT introduce new semantic claims beyond evidence.
3. Shim behavior MUST be deterministic across lowering runs.

[Evidence: LESSONS_LEARNED:lesson-9-runtime-shim-must-be-semantically-justified]

### 10.9 ISIL Backend Infrastructure

The existing ISIL backend provides infrastructure orthogonal to P-CIL semantic recovery. Use is OPTIONAL.

| Component | Role |
|---|---|
| CFG construction | Build control-flow graph for structured CIL emission |
| SSA transformation | Enable optimization and variable liveness |
| Stackification | Convert register-form to CIL evaluation-stack form |
| Verifier-safe emission | Mechanics for producing ECMA-335-compliant bytecode |

Rules:
1. Use of ISIL infrastructure is OPTIONAL.
2. If used, it serves as mechanical backend only; semantic decisions are governed by this specification.
3. A lowering pass MUST NOT reintroduce ISIL-level call heuristics as a substitute for the evidence-based recovery model.

[Evidence: ISIL_RETIREMENT:backend-infrastructure-remains-valuable]
[Evidence: DECISION_SUMMARY:isil-demoted-to-backend-substrate]

### 10.10 Source Anchors

| Anchor | Used In |
|---|---|
| [Evidence: ISIL_RETIREMENT:downstream-lowering-role] | 10.1 |
| [Evidence: ISIL_RETIREMENT:backend-infrastructure-remains-valuable] | 10.3, 10.9 |
| [Evidence: LESSONS_LEARNED:lesson-2-conservative-failure-better-than-wrong-recovery] | 10.4.3, 10.6 |
| [Evidence: LESSONS_LEARNED:lesson-8-recognized-but-fallback-is-valuable] | 10.4.2 |
| [Evidence: LESSONS_LEARNED:lesson-9-runtime-shim-must-be-semantically-justified] | 10.8 |
| [Evidence: LESSONS_LEARNED:lesson-12-fact-preservation-beats-heuristic-sophistication] | 10.4.1 |
| [Evidence: DECISION_SUMMARY:isil-demoted-to-backend-substrate] | 10.9 |

---

## Chapter 11: Conformance And Evidence

### 11.1 Purpose

This chapter defines conformance levels for P-CIL producers and consumers, evidence sufficiency criteria, verification methodology, and progress measurement. A conforming P-CIL producer MUST declare which conformance level it targets.

### 11.2 Conformance Levels

The conformance ladder is grounded in the recovery domains defined throughout this specification. Each level references the normative chapters that define its requirements. The level taxonomy itself is a specification-internal design decision based on the staged formalization strategy.

[Evidence: FORMALIZATION_REQUIREMENTS:staged-formalization-strategy]
[Evidence: LESSONS_LEARNED:lesson-3-narrow-canonical-slices-dont-scale]

#### 11.2.1 L0 Base

CFG + P-Code/HighFunction export (Chapter 2). Recovery objects with state tracking (Chapter 3). No overlay required. This level validates that the producer can interface with the host substrate and emit structurally valid P-CIL containers.

#### 11.2.2 L1 Checks

L0 + null, bounds, div-zero, array-store, overflow check recovery (Chapter 7). Profile-aware check modeling (Chapter 7.4). Checks are the simplest recovery domain (symbol-driven, no cross-reference needed), making them a natural first validation target.

#### 11.2.3 L2 Managed Overlay

L1 + array/field/box/unbox/cast recovery (Ch 4) + call classification into protocol families (Ch 5) + metadata initialization (Annex A) + RGCTX access (Annex C.4) + class init (Annex A.4.4) + string literals (Ch 4.4.9). This level validates recovery of the managed semantic overlay.

#### 11.2.4 L3 Stateful

L2 + state machine tracking (ClassInitState, MetaSlotState, RgctxState, ExceptionState per Ch 3.8) + exception edges (Ch 6) + carrier lifecycle tracking (Annex C.8). This level validates tracking of stateful IL2CPP protocols across function bodies.

#### 11.2.5 L4 Verified

L3 + passes minimal test baseline (11.4) + traceable evidence chain per overlay + verifier-safe CIL output for resolved objects (Ch 10) + active progress measurement (11.5). This is the production-quality target.

### 11.3 Evidence Sufficiency Per Level

| Level | Domain | Min Confidence for "Recovered" | Min Anchor Requirement |
|---|---|---|---|
| L0 | CFG / substrate export | N/A (no overlays) | Substrate source only |
| L1 | Check overlays (Ch 7) | Moderate (>= 0.50) per check | Symbol or pattern anchor per check |
| L2 | Value/call/metadata overlays (Ch 4, 5, Annex A) | Strong (>= 0.80) for resolved overlays | Symbol + metadata or pattern + metadata per overlay |
| L3 | State machine transitions (Ch 3.8) | Strong (>= 0.80) for each transition | Control-template anchor for state machine patterns |
| L4 | All domains + verification | Strong (>= 0.80) + traceable IL2CPP source anchor | Full evidence chain: substrate -> pattern/symbol -> IL2CPP source |

### 11.4 Verification Baseline

Not a fixed test set. Methodology-based:

**Test categories**: synthetic (hand-crafted), sampled real (from IL2CPP binaries), regression (previously-failed cases).

**Pass criteria**: (1) resolved objects produce correct CIL, (2) no provably wrong resolved objects, (3) partial/unresolved correctly classified, (4) output is verifier-safe.

**Adequacy**: covers every recovery rule at the claimed level + at least one negative case per domain.

[Evidence: LESSONS_LEARNED:lesson-6-ground-truth-changed-improvement]

### 11.5 Progress Measurement

**Source-owned buckets**: measure primarily on application methods, not vendor assemblies.

**Primary metrics**: exact token match, exact opcode match against ground truth.

**Diagnostic metrics**: recovery rate, bucket distribution, fallback rate, low-recovery sample list.

**Not valid primary metrics**: converted method count without quality, non-stub count alone, pattern hit rate without confidence, build success alone.

[Evidence: LESSONS_LEARNED:lesson-7-source-owned-progress-matters-more]

### 11.6 Conformance Declaration

A producer MUST declare: target level (L0--L4), architecture scope, profile scope, known limitations. MAY claim different levels per domain if made explicit.

### 11.7 Source Anchors

| Anchor | Used In |
|---|---|
| [Evidence: FORMALIZATION_REQUIREMENTS:staged-formalization-strategy] | 11.2 Conformance level taxonomy design |
| [Evidence: LESSONS_LEARNED:lesson-3-narrow-canonical-slices-dont-scale] | 11.2 Why staged levels, not all-at-once |
| [Evidence: LESSONS_LEARNED:lesson-6-ground-truth-changed-improvement] | 11.4 Verification baseline methodology |
| [Evidence: LESSONS_LEARNED:lesson-7-source-owned-progress-matters-more] | 11.5 Progress measurement |

---

## Annex A: Metadata Initialization Protocol

### A.1 Purpose

This annex defines how P-CIL models the IL2CPP metadata initialization protocol: the process by which lazily-initialized metadata slots and RGCTX arrays transition from uninitialized to initialized state.

In CIL, instructions reference types, methods, fields, and strings via statically-resolved metadata tokens. IL2CPP replaces this with a lazy initialization protocol: each method body carries an initialization guard and a set of global pointer slots, each pre-populated with an encoded token. On first invocation, the guard triggers batch initialization that decodes each token into a live runtime pointer.

The recovery task is to recognize this initialization scaffolding, track the state of each slot, and ultimately elide the scaffolding when lowering to CIL.

### A.2 Semantic Target

**CIL-level**: metadata tokens are statically known; no initialization code exists in method bodies.

**IL2CPP divergence**: Three key properties:
1. **Encoded-token representation.** Each metadata reference is a global `uintptr_t` initialized to an encoded 32-bit token (bit 0 = 1). Encoding packs a 3-bit usage type and 28-bit table index.
2. **Lazy atomic resolution.** On first access, runtime resolves to a live pointer and publishes via atomic store.
3. **Per-method guard.** Boolean guard ensures batch initialization runs at most once.

### A.3 Evidence Sources

| ID | Evidence Source | Generated-Code Surface | Runtime Entry Point |
|----|---------------|----------------------|-------------------|
| E-A.1 | Batch init guard (non-generic) | `if (!s_Il2CppMethodInitialized) { ... }` | `il2cpp_codegen_initialize_runtime_metadata` |
| E-A.2 | Batch init guard (generic, RGCTX) | `if (!il2cpp_rgctx_is_initialized(method)) { ... }` | `il2cpp_rgctx_method_init` |
| E-A.3 | Inline metadata init | `(T*)il2cpp_codegen_initialize_runtime_metadata_inline(&slot)` | `MetadataCache::InitializeRuntimeMetadata` |
| E-A.4 | RGCTX data access (init) | `il2cpp_rgctx_data(rgctxVar, index)` | `InitializedTypeInfo(...)` |
| E-A.5 | RGCTX data access (no_init) | `il2cpp_rgctx_data_no_init(rgctxVar, index)` | Direct field read |
| E-A.6 | Class init | `il2cpp_codegen_runtime_class_init_inline(klass)` | `Runtime::ClassInit` |

[Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:init-metadata-functions]
[Source: il2cpp/libil2cpp/vm/GlobalMetadata.cpp:InitializeRuntimeMetadata]

### A.4 Recovery Rules

#### A.4.1 Batch Metadata Initialization (Per-Method Init Guard)

**Pattern (non-generic):**
```c
static bool s_Il2CppMethodInitialized;
if (!s_Il2CppMethodInitialized) {
    il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&slot_1);
    il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&slot_2);
    s_Il2CppMethodInitialized = true;
}
```

**Pattern (generic with RGCTX):**
```c
if (!il2cpp_rgctx_is_initialized(method)) {
    il2cpp_codegen_initialize_runtime_metadata((uintptr_t*)&slot_1);
    il2cpp_rgctx_method_init(method);
}
```

**Recovery rule:**
- **Precondition**: Boolean-guarded block at method entry.
- **Evidence**: Known init helper calls within guarded block.
- **State transitions**: For each slot: `MetaSlotState[slot]: Unknown|EncodedToken -> InitializedPtr`. For RGCTX: `RgctxState[method]: Unknown -> Initialized`.
- **Post-state**: All referenced slots initialized for remainder of method.

[Source: il2cpp/Unity.IL2CPP/CodeWriters/CodeWriterExtensions.cs:WriteMethodMetadataInitialization]
[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:metadata-init-tracking]

#### A.4.2 Inline Metadata Initialization

**Pattern:**
```c
((RuntimeClass*)il2cpp_codegen_initialize_runtime_metadata_inline((uintptr_t*)&slot))
```

**Recovery rule:**
- **Precondition**: Call to known inline init function in expression context.
- **Evidence**: Single operand is pointer to metadata global slot.
- **State transition**: `MetaSlotState[slot]: Unknown|EncodedToken -> InitializedPtr`.
- **Post-state**: Return value is initialized pointer; slot remains initialized.
- Underlying function is idempotent: already-initialized slots return immediately.

[Source: il2cpp/Unity.IL2CPP/DefaultRuntimeMetadataAccess.cs:FormatRuntimeIdentifier]

#### A.4.3 RGCTX Initialization

Two access policies per Annex C.4.3:

**init policy** (`il2cpp_rgctx_data`): Calls `InitializedTypeInfo(rgctxVar[index].klass)`, ensuring class is fully initialized before return.

**no_init policy** (`il2cpp_rgctx_data_no_init`): Reads RGCTX slot directly; no class initialization triggered. Used for `sizeof`, field offset, `IsValueType` checks.

**Recovery rule:**
- **Precondition**: Call to known RGCTX accessor with base expression + integer index.
- **State transitions (init)**: `RgctxState -> Initialized`; `ClassInitState[type] -> Done`.
- **State transitions (no_init)**: No state change.

The `no_init` variant is selected when `TypeInfoForReason` is Size, Field, IsValueType, Box, or WouldBoxToNull.

[Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:rgctx-accessors]
[Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:TypeInfoFor-with-reason]

#### A.4.4 Class Initialization (Static Constructor)

**Pattern:**
```c
il2cpp_codegen_runtime_class_init_inline(klass);
```

**Recovery rule:**
- **Precondition**: Call to known class-init function with type-info operand.
- **State transitions**: `ClassInitState[type]: Unknown|NotStarted -> Running -> Done` (success) or `-> Failed` (exception cached).
- **Failure semantics**: `ClassInitState.Failed` is sticky; cached exception re-raised on subsequent attempts.
- **Codegen optimization**: `_classesAlreadyInitializedInBlock` avoids redundant init calls within same block. Recovery MAY use dominator analysis for same purpose.

[Source: il2cpp/libil2cpp/vm/Runtime.cpp:ClassInit]
[Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:classesAlreadyInitializedInBlock]

**Cross-reference**: Full class initialization protocol deferred to Annex B (Round 2).

### A.5 Encoded Token Format

Each metadata global slot carries an encoded 32-bit value:

```
Bits [31:29]  Usage type (Il2CppMetadataUsage, 3 bits)
Bits [28:1]   Table index (28 bits)
Bit  [0]      Always 1 (init sentinel)
```

Encoding: `encoded = (usage_type << 29) | (table_index << 1) | 1`
Decoding: `usage_type = (encoded >> 29) & 0x7; table_index = (encoded >> 1) & 0x1FFFFFFF`

Usage types (Annex C.6.3):

| Value | Name | Resolved Type |
|---|---|---|
| 1 | TypeInfo | `Il2CppClass*` |
| 2 | Il2CppType | `const Il2CppType*` |
| 3 | MethodDef | `const MethodInfo*` |
| 4 | FieldInfo | `FieldInfo*` |
| 5 | StringLiteral | `Il2CppString*` |
| 6 | MethodRef | `const MethodInfo*` (generic) |
| 7 | FieldRva | `void*` |

[Source: il2cpp/libil2cpp/vm/GlobalMetadataFileInternals.h:encoded-token]

### A.6 Recovery Forms

- **Resolved**: Init pattern fully matched; all slots identified; state transitions tracked; encoded tokens decoded to usage type + table index.
- **Partial**: Init pattern detected but some slots undecoded (metadata tables unavailable) or guard boundary ambiguous.
- **Unresolved**: Memory access pattern resembles metadata init but cannot be confirmed; no recognized guard or helper.

### A.7 Confidence And Ambiguity

| Pattern | Level | Range |
|---------|-------|-------|
| Batch init guard + known helpers | Definitive | 0.95--1.0 |
| Inline init via known helper | Strong | 0.85--0.94 |
| RGCTX access via known accessor | Strong | 0.85--0.94 |
| Class init via known helper | Strong | 0.80--0.90 |
| Init-like pattern without confirmed helper | Moderate | 0.50--0.70 |
| Unrecognized guard with pointer dereferences | Weak | 0.20--0.40 |

Ambiguity is rare for metadata init (patterns are structurally distinctive). Mainly arises when helpers are inlined and patterns fragment.

### A.8 Lowering Obligations

**Resolved**: Init guard block SHOULD be elided entirely (no CIL equivalent). Each metadata slot reference replaced with the corresponding CIL metadata token based on usage type. RGCTX accesses resolved to generic type/method bindings.

**Partial**: Guard MAY be elided if confirmed. Unresolved slots emitted as placeholder tokens with diagnostic.

**Unresolved**: Emitted as `recognized-but-fallback` opaque helper or `nop` with diagnostic.

**Class init**: `il2cpp_codegen_runtime_class_init_inline` calls SHOULD be elided (CLR handles `.cctor` transparently). If type cannot be determined, preserve as `recognized-but-fallback`.

### A.9 Source Anchors

| Anchor | Description |
|---|---|
| [Source: il2cpp/libil2cpp/vm/GlobalMetadata.cpp:InitializeRuntimeMetadata] | Core slot resolution with atomic read/publish |
| [Source: il2cpp/libil2cpp/vm/GlobalMetadataFileInternals.h:encoded-token] | Encoded token bit layout |
| [Source: il2cpp/libil2cpp/vm/GlobalMetadata.h:IsRuntimeMetadataInitialized] | Bit-0 init check |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:rgctx-accessors] | RGCTX accessor functions |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:init-metadata-functions] | Init entry points |
| [Source: il2cpp/libil2cpp/vm/Runtime.cpp:ClassInit] | Class init protocol |
| [Source: il2cpp/Unity.IL2CPP/CodeWriters/CodeWriterExtensions.cs:WriteMethodMetadataInitialization] | Codegen batch init emission |
| [Source: il2cpp/Unity.IL2CPP/DefaultRuntimeMetadataAccess.cs:FormatRuntimeIdentifier] | Inline init pattern |
| [Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:TypeInfoFor-with-reason] | Init/no_init policy selection |
| [Source: il2cpp/Unity.IL2CPP/MethodBodyWriter.cs:classesAlreadyInitializedInBlock] | Class-init dedup |

**Cross-references**: Annex C.6 (metadata globals), Annex C.4 (RGCTX), Annex B (class init, Round 2), Chapter 5 (call semantics), Chapter 4 (value/data model).

---

## Annex B: Class Initialization Protocol

### B.1 Purpose

This annex will define the full IL2CPP class initialization protocol: the process by which a type's static constructor (`.cctor`) is invoked, the thread-safety model, reentrant detection, failure caching, and re-raise behavior.

### B.2 Semantic Target

CIL: implicit `.cctor` invocation per ECMA-335 II.10.5.3. IL2CPP makes this explicit via `il2cpp_codegen_runtime_class_init`.

### B.3 Intended Content (Outline)

- **ClassInitState machine** (defined in Chapter 3.8.1): Unknown -> NotStarted -> Running -> Done | Failed
- **Thread-safety**: IL2CPP uses locking (`il2cpp::os::FastAutoLock`) to prevent concurrent init
- **Reentrant detection**: `currentThreadIsInitializing` check prevents infinite recursion
- **Failure caching**: `initializationExceptionGCHandle` stores exception; re-raised on subsequent access
- **Codegen-side dedup**: `_classesAlreadyInitializedInBlock` tracks init within method (Annex A.4.4)
- **Recovery implications**: init call must be traced to determine type; init-before-access ordering must be preserved
- **Lowering**: class init calls elided (CLR handles `.cctor` transparently); preserved as fallback when type unknown

[Source: il2cpp/libil2cpp/vm/Runtime.cpp:ClassInit]

> Reserved: This annex ships as outline-only in v1. Full formalization requires detailed analysis of the thread-safety and reentrant-detection protocols in Runtime.cpp, which interacts with the OS threading layer. The ClassInitState machine in Chapter 3.8.1 provides the recovery-side model; this annex will complete the protocol-side specification.

---

## Annex C: Carrier Protocol Reference

### C.1 Purpose

This annex defines the **carrier** objects that the IL2CPP runtime uses to transfer metadata through call dispatch sequences. A carrier bridges the gap between the type-erased native calling convention and the rich CIL type system that the decompiler must recover.

Chapters 5 (Call Semantics) and Annex A (Metadata Initialization) reference carrier definitions normatively. This annex is the single authoritative definition point; those chapters SHALL NOT redefine carrier structure or semantics, but MUST cross-reference this annex by section number.

**Fundamental pointer distinction.** Two pointer types pervade every carrier and MUST NOT be conflated:

| Pointer Type | C Type | Role |
|---|---|---|
| **Method pointer** | `Il2CppMethodPointer` | Executable code address; the function to call |
| **Metadata pointer** | `const MethodInfo*` (alias `const RuntimeMethod*`) | Method descriptor; carries type information, slot, generic context, and invoker |

A method pointer is a bare function address. A metadata pointer is a struct pointer containing the method pointer as one of its fields (`MethodInfo::methodPointer`), alongside generic context (`rgctx_data`), slot number, parameter types, and the invoker trampoline (`invoker_method`). Every carrier that participates in call dispatch carries one or both of these; recovery MUST track which role each observed pointer serves.

[Source: il2cpp/libil2cpp/il2cpp-class-internals.h:MethodInfo-struct]

### C.2 Carrier Taxonomy

The IL2CPP codegen emits five categories of carrier object:

| Carrier | Purpose | Transfer Mechanism | Primary Consumers |
|---|---|---|---|
| **MethodInfo** | Hidden method descriptor for generic context and reflection | Final parameter in direct calls | Callee method body, RGCTX access |
| **RGCTXData** | Runtime generic context: types, methods, fields resolved at runtime for shared generic code | Indexed read from `rgctx_data` array on class or method | Generic instantiation, type checks, allocations |
| **VirtualInvokeData** | Virtual/interface dispatch resolution: method pointer + metadata pair | Runtime lookup function returning struct | Virtual and interface call sites |
| **Metadata Global** | Lazily-initialized metadata slot: type info, method info, field info, string literal | Global variable with atomic publish | Any metadata reference in non-generic code |
| **Delegate** | Delegate invocation context: target, binding mode, stub selection | Fields on delegate object, set during `.ctor` | `Invoke`, `BeginInvoke`, multicast dispatch |

**Carrier vs. value.** A carrier is not the metadata itself -- it is the *protocol object that transfers* metadata from a source (runtime table, vtable, global slot) to a consumption site (call instruction, type check, allocation). Recovery recovers carriers, then extracts the CIL semantic from the carrier's content.

**Recovery status.** Every carrier instance MUST be classified as **resolved**, **partial**, or **unresolved** per Chapter 3.

### C.3 MethodInfo Carrier

#### C.3.1 Definition

The MethodInfo carrier is a hidden parameter of type `const RuntimeMethod*` appended as the final argument in direct (non-virtual, non-invoker) call signatures. It provides the callee with its own method descriptor, enabling generic context access, reflection, and runtime type resolution.

[Source: il2cpp/Unity.IL2CPP/MethodSignatureWriter.cs:FormatHiddenMethodArgument]

#### C.3.2 Presence Condition

The hidden MethodInfo parameter is present if and only if `NeedsHiddenMethodInfo()` returns true. The decision tree:

1. If the method is remapped by `IntrinsicRemap` and `StillNeedsHiddenMethodInfo()` returns false: **absent**.
2. If the call type is `Virtual` and the method cannot be devirtualized (`IsVirtual && !DeclaringType.IsSealed && !IsFinal`): **absent** -- virtual dispatch provides metadata via `VirtualInvokeData.method` instead.
3. Otherwise, delegate to `NeedsHiddenMethodInfoForDefinition()`:
   - TinyBackend: **absent**.
   - Array special methods (`ArrayNaming.IsSpecialArrayMethod`): **absent**.
   - `GetOrSetGenericValueOnArray`: **absent**.
   - Generic instances of `Interlocked.CompareExchange<T>` or `Interlocked.Exchange<T>`: **absent**.
   - All other cases: **present**.

[Source: il2cpp/Unity.IL2CPP/MethodSignatureWriter.cs:NeedsHiddenMethodInfo]

#### C.3.3 Placement

The MethodInfo parameter is always the *last* parameter in the signature, appended after all explicit parameters and any by-ref return value parameter (`il2cppRetVal`).

[Source: il2cpp/Unity.IL2CPP/MethodSignatureWriter.cs:ParametersForInternal]

#### C.3.4 Content Variants

| Context | Value | Source |
|---|---|---|
| Non-generic method in non-generic type | `NULL` | `DefaultRuntimeMetadataAccess.HiddenMethodInfo` |
| Generic instance method or method on generic type | Pointer to initialized `RuntimeMethod*` from metadata global slot | `DefaultRuntimeMetadataAccess.HiddenMethodInfo` via metadata global |
| Shared generic code, self-reference | `method` (pass-through of own hidden param) | `SharedRuntimeMetadataAccess.HiddenMethodInfo` |
| Shared generic code, other method | RGCTX lookup via `il2cpp_rgctx_method(...)` | `SharedRuntimeMetadataAccess.HiddenMethodInfo` via RGCTX |

[Source: il2cpp/Unity.IL2CPP/DefaultRuntimeMetadataAccess.cs:HiddenMethodInfo]
[Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:HiddenMethodInfo]

#### C.3.5 Recovery Forms

- **Resolved**: Presence confirmed (parameter count matches, final parameter is pointer-sized non-data argument); value traced to `NULL`, a known metadata global, or a known RGCTX index. Evidence level: Definitive when symbol/metadata confirms parameter count; Strong when inferred from calling convention.
- **Partial**: Presence confirmed but value cannot be traced to a specific method descriptor. Common when the hidden parameter's origin is an opaque register load. Evidence level: Moderate to Strong.
- **Unresolved**: Cannot determine whether the final parameter is a hidden MethodInfo or a regular argument. Evidence level: Weak to Insufficient.

### C.4 RGCTXData Carrier

#### C.4.1 Definition

The Runtime Generic Context (RGCTX) data carrier provides shared generic methods with type-specific metadata at runtime. Rather than monomorphizing every generic instantiation, IL2CPP compiles a single shared implementation that reads instantiation-specific types, methods, and fields from an RGCTX array.

```c
typedef union Il2CppRGCTXData {
    void*              rgctxDataDummy;
    const MethodInfo*  method;
    const Il2CppType*  type;
    Il2CppClass*       klass;
} Il2CppRGCTXData;
```

[Source: il2cpp/libil2cpp/il2cpp-class-internals.h:Il2CppRGCTXData]

#### C.4.2 Access Patterns

Three base expressions provide access to the RGCTX array:

| Access Pattern | Base Expression | Condition |
|---|---|---|
| **Type-level** | `InitializedTypeInfo(method->klass)->rgctx_data` | `!HasThis \|\| DeclaringType.IsValueType` |
| **This-level** | `method->klass->rgctx_data` | `HasThis && !DeclaringType.IsValueType` |
| **Method-level** | `method->rgctx_data` | Generic context includes method-level generic parameters |

[Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:GetRGCTXAccess]

#### C.4.3 Initialization Policies

| Function | Behavior |
|---|---|
| `il2cpp_rgctx_data(rgctxVar, index)` | Returns `Il2CppClass*` with class initialization |
| `il2cpp_rgctx_data_no_init(rgctxVar, index)` | Returns `Il2CppClass*` without initialization |
| `il2cpp_rgctx_type(rgctxVar, index)` | Returns `const Il2CppType*` |
| `il2cpp_rgctx_method(rgctxVar, index)` | Returns `const MethodInfo*` |

The `no_init` variant is selected when the caller needs only the class pointer without guaranteeing static constructor execution.

[Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:rgctx-accessors]

#### C.4.4 Item Kinds

| Kind | Union Member | Semantic |
|---|---|---|
| `Class` | `klass` | Class metadata for type checks, allocation, static data |
| `Type` | `type` | IL2CPP type representation for reflection |
| `Static` | `klass` | Specifically for static field access context |
| `Array` | `klass` | Array element type class |
| `Method` | `method` | Method descriptor for call or reflection |
| `Constrained` | `method` | Constrained call resolution |

[Source: il2cpp/Unity.IL2CPP/GenericSharing/RuntimeGenericContextInfo.cs:enum]

#### C.4.5 Recovery Forms

- **Resolved**: Access pattern, RGCTX base expression, index, init policy, and item kind are all determined. Evidence level: Definitive when metadata tables provide RGCTX layout; Strong when inferred from access pattern + index.
- **Partial**: RGCTX access pattern identified but index is a runtime variable, init policy cannot be distinguished, or item kind cannot be determined from consumption context. Evidence level: Moderate.
- **Unresolved**: Indexed memory access through a struct chain observed but cannot be confirmed as RGCTX access. Evidence level: Weak to Insufficient.

### C.5 VirtualInvokeData Carrier

#### C.5.1 Definition

The VirtualInvokeData carrier resolves virtual and interface method dispatch at runtime:

```c
typedef struct VirtualInvokeData {
    Il2CppMethodPointer methodPtr;   // executable code address
    const MethodInfo*   method;      // method descriptor (metadata)
} VirtualInvokeData;
```

[Source: il2cpp/libil2cpp/il2cpp-class-internals.h:VirtualInvokeData]

#### C.5.2 Creation Functions

| Function | Dispatch Category | Semantics |
|---|---|---|
| `il2cpp_codegen_get_virtual_invoke_data(slot, obj)` | Non-generic virtual | Returns reference to `obj->klass->vtable[slot]` |
| `il2cpp_codegen_get_interface_invoke_data(slot, obj, interface)` | Non-generic interface | Vtable lookup via interface offset |
| `il2cpp_codegen_get_generic_virtual_invoke_data(method, obj, &invokeData)` | Generic virtual | Inflates method from vtable slot |
| `il2cpp_codegen_get_generic_interface_invoke_data(method, obj, &invokeData)` | Generic interface | Inflates interface method |

Non-generic variants return a *reference* to a vtable entry (zero-copy); generic variants write to a caller-provided struct (requires inflation).

[Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:virtual-lookup]

#### C.5.3 Consumption Patterns

**Direct consumption (non-invoker path):**
```c
((FunctionPointerType)invokeData.methodPtr)(obj, p1, ..., pN, invokeData.method);
```

**Invoker consumption (generic/shared path):**
```c
invokeData.method->invoker_method(
    il2cpp_codegen_get_method_pointer(invokeData.method),
    invokeData.method, obj, params[], retVal);
```

[Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:consumption-patterns]

#### C.5.4 Recovery Forms

- **Resolved**: Dispatch category determined, input parameters known, consumption pattern identified. Evidence level: Definitive when lookup function call is matched; Strong when vtable access pattern is structurally confirmed.
- **Partial**: VirtualInvokeData creation observed but consumption site cannot be located, OR consumption observed but creation origin is in an unanalyzed predecessor. Evidence level: Moderate.
- **Unresolved**: Indirect call through a struct-derived function pointer observed but VirtualInvokeData origin not confirmed. Evidence level: Weak to Insufficient.

### C.6 Metadata Globals (Encoded Slots)

#### C.6.1 Definition

Metadata globals are statically-allocated `uintptr_t` slots that the IL2CPP runtime lazily initializes to point at metadata objects. Before initialization, each slot contains an **encoded token** with the low bit set to 1; after initialization, the slot contains an aligned pointer with the low bit 0.

[Source: il2cpp/libil2cpp/vm/GlobalMetadata.h:IsRuntimeMetadataInitialized]

#### C.6.2 Encoded Token Format

```
Bits [31:29]  Usage type (Il2CppMetadataUsage enum, 3 bits)
Bits [28:1]   Decoded index (28 bits)
Bit  [0]      Always 1 (uninitialized marker)
```

[Source: il2cpp/libil2cpp/vm/GlobalMetadataFileInternals.h:encoded-token]

#### C.6.3 Usage Types

| Usage Enum | Value | Decoded Object |
|---|---|---|
| `kIl2CppMetadataUsageTypeInfo` | 1 | `Il2CppClass*` (RuntimeClass) |
| `kIl2CppMetadataUsageIl2CppType` | 2 | `const Il2CppType*` (RuntimeType) |
| `kIl2CppMetadataUsageMethodDef` | 3 | `const MethodInfo*` (method definition) |
| `kIl2CppMetadataUsageFieldInfo` | 4 | `FieldInfo*` (RuntimeField) |
| `kIl2CppMetadataUsageStringLiteral` | 5 | `Il2CppString*` (String_t) |
| `kIl2CppMetadataUsageMethodRef` | 6 | `const MethodInfo*` (generic method reference) |
| `kIl2CppMetadataUsageFieldRva` | 7 | `void*` (field RVA data pointer) |

[Source: il2cpp/libil2cpp/vm/GlobalMetadataFileInternals.h:Il2CppMetadataUsage]

#### C.6.4 Initialization Protocol

The initialization flow is lock-free and idempotent:

1. **Atomic read**: `metadataValue = Atomic::ReadPtrVal(metadataPointer)`
2. **Init check**: If `(metadataValue & 1) == 0`, already initialized; return `(void*)metadataValue`
3. **Decode**: Extract usage type and index from the 32-bit encoded token
4. **Resolve**: Switch on usage type to obtain the runtime metadata object
5. **Atomic publish**: `Atomic::PublishPointer(metadataPointer, initialized)`
6. **Memory barrier**: Full memory barrier after publish (in the batch path)

Two entry points exist:
- `il2cpp_codegen_initialize_runtime_metadata(uintptr_t* slot)` -- batch initialization in method init guard
- `il2cpp_codegen_initialize_runtime_metadata_inline(uintptr_t* slot)` -- inline initialization at expression site

[Source: il2cpp/libil2cpp/vm/GlobalMetadata.cpp:InitializeRuntimeMetadata]

#### C.6.5 Recovery Forms

- **Resolved**: Slot address identified, initialization status determined, usage type and decoded index extracted, target metadata object known. Evidence level: Definitive when metadata registration table is available; Strong when init helper call is pattern-matched.
- **Partial**: Init helper call observed with identifiable slot address, but encoded token cannot be decoded. Evidence level: Moderate.
- **Unresolved**: Global variable access observed but cannot be confirmed as a metadata slot. Evidence level: Weak to Insufficient.

### C.7 Delegate Carriers

#### C.7.1 Definition

Delegate carriers encode the full invocation context for CIL delegate objects. A delegate in IL2CPP is not a simple function pointer; it is a managed object whose fields encode the target method, binding mode, and the invocation stub.

[Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-fields]

#### C.7.2 Core Fields

| Field Name | C Type | Role |
|---|---|---|
| `method_ptr` | `Il2CppMethodPointer` | Actual target function pointer |
| `invoke_impl` | `Il2CppMethodPointer` | Selected invocation stub |
| `method` | `const RuntimeMethod*` | Method metadata descriptor |
| `m_target` | `Il2CppObject*` | Delegate target instance; NULL for open static |
| `method_is_virtual` | `bool` | Whether target requires virtual dispatch |
| `extra_arg` | `void*` | Multicast invoke stub pointer |
| `method_code` | `void*` | Stores `this` pointer for closed instance delegates |

[Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-ctor]

#### C.7.3 Stub Selection Logic

The `invoke_impl` field is set during construction based on binding mode:

**Static methods:**

| Condition | Stub |
|---|---|
| Open (paramCount matches Invoke) | `OpenStatic` / `OpenStaticInvoker` |
| Closed | `ClosedStatic` / `ClosedStaticInvoker` |

**Instance methods:**

| Condition | Stub |
|---|---|
| Open + virtual + generic interface | `OpenGenericInterface` / `OpenGenericInterfaceInvoker` |
| Open + virtual + generic + !interface | `OpenGenericVirtual` / `OpenGenericVirtualInvoker` |
| Open + virtual + !generic + interface | `OpenInterface` / `OpenInterfaceInvoker` |
| Open + virtual + !generic + !interface | `OpenVirtual` / `OpenVirtualInvoker` |
| Open + !virtual | `OpenInst` / `OpenInstInvoker` |
| Closed (target != null) | `ClosedInst` / `ClosedInstInvoker` |

The `Invoker` suffix variants are used when `CallDelegatesViaInvokers` is true (full generic sharing mode).

[Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-stub-selection]

#### C.7.4 Invocation Protocol

**Single delegate:**
```c
((FunctionPointerType)__this->invoke_impl)(
    (Il2CppObject*)__this->method_code, p1, ..., pN,
    (RuntimeMethod*)__this->method);
```

**Multicast:** Loops over `delegates` array, calls `Invoke` on each, returns value of last delegate.

[Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-invoke]

#### C.7.5 Recovery Forms

- **Resolved**: Delegate type identified; binding mode determined; target method known; all fields populated. Evidence level: Definitive when delegate `.ctor` call site is fully analyzed; Strong when stub function address matches known delegate stubs.
- **Partial**: Delegate construction observed (field writes to carrier fields) but stub selection or target uncertain. Evidence level: Moderate.
- **Unresolved**: Indirect call through object field observed but delegate nature not confirmed. Evidence level: Weak to Insufficient.

### C.8 Carrier Lifecycle Model

Every carrier instance passes through three lifecycle stages: **creation**, **transfer**, and **consumption**. Full resolution requires evidence at all three stages.

#### C.8.1 General Rules

1. A carrier MUST be traced from creation to consumption for **end-to-end resolved** status.
2. If creation is observed but consumption is not, the carrier is **partial**.
3. If consumption is observed but creation origin is unknown, the carrier is **partial**.
4. If neither creation nor consumption can be linked, the carrier is **unresolved**.
5. Transfer through SSA-traceable locals does not degrade evidence. Transfer through memory (heap store, global) MAY degrade evidence if aliasing cannot be excluded.
6. **Call-site resolution exception**: For the purpose of call recovery (Chapter 5), a carrier is **call-site resolved** when its transfer at the call boundary is confirmed (e.g., MethodInfo confirmed as final parameter, VirtualInvokeData confirmed as source of indirect call target) even if the callee's internal consumption cannot be observed. Call-site resolution is sufficient for Chapter 5 resolved call status. End-to-end resolution is required only when interprocedural carrier propagation is being modeled (e.g., tracing RGCTX data through nested calls).

#### C.8.2 Per-Carrier Lifecycle Summary

| Carrier | Creation | Transfer | Consumption |
|---|---|---|---|
| **MethodInfo** | Metadata global load, RGCTX method access, or `NULL` | Final argument in direct call; may be forwarded | Callee reads for RGCTX access, reflection, or pass-through |
| **RGCTXData** | Field on `Il2CppClass` or `MethodInfo` | Indexed read `rgctx_data[index]` to local | Argument to allocation, type check, method call |
| **VirtualInvokeData** | Runtime lookup function | Struct stored to local | `methodPtr` extracted for call; or `invoker_method` path |
| **Metadata Global** | Encoded token in global slot | Init call atomically publishes pointer | Cast to typed metadata pointer |
| **Delegate** | `.ctor` writes fields | Object passed as argument or stored to field | `Invoke` reads `invoke_impl` + `method_code` + `method` |

#### C.8.3 Cross-Carrier Dependencies

- **VirtualInvokeData -> MethodInfo**: The `method` field of `VirtualInvokeData` carries the same `const MethodInfo*` that would otherwise be the hidden parameter. When a virtual call uses the invoker path, `VirtualInvokeData.method` replaces the MethodInfo carrier's transfer mechanism.
- **Delegate -> VirtualInvokeData**: Open virtual delegates use virtual dispatch internally. The `OpenVirtual` / `OpenInterface` stubs invoke lookup functions during `Invoke`, creating a VirtualInvokeData carrier.
- **RGCTXData -> MethodInfo**: In shared generic code, the MethodInfo carrier's value is sourced from an RGCTX method slot (`il2cpp_rgctx_method(...)`).
- **Metadata Global -> MethodInfo / RGCTXData**: In non-generic code, the MethodInfo carrier's value is loaded from a metadata global slot.

### C.9 Source Anchors

**IL2CPP Codegen (Unity.IL2CPP):**

| Anchor | Region |
|---|---|
| [Source: il2cpp/Unity.IL2CPP/MethodSignatureWriter.cs:NeedsHiddenMethodInfo] | `NeedsHiddenMethodInfo()`, `NeedsHiddenMethodInfoForDefinition()` |
| [Source: il2cpp/Unity.IL2CPP/MethodSignatureWriter.cs:FormatHiddenMethodArgument] | Parameter formatting |
| [Source: il2cpp/Unity.IL2CPP/MethodSignatureWriter.cs:ParametersForInternal] | Parameter ordering |
| [Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:GetRGCTXAccess] | Access pattern selection |
| [Source: il2cpp/Unity.IL2CPP/SharedRuntimeMetadataAccess.cs:HiddenMethodInfo] | Shared generic hidden param |
| [Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:virtual-dispatch] | Virtual/interface dispatch emission |
| [Source: il2cpp/Unity.IL2CPP/InterfaceAndVirtualInvokeWriter.cs:consumption-patterns] | Direct vs invoker consumption |
| [Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-stub-selection] | Stub selection logic |
| [Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-ctor] | Delegate constructor |
| [Source: il2cpp/Unity.IL2CPP/DelegateMethodsWriter.cs:delegate-invoke] | Delegate invocation |
| [Source: il2cpp/Unity.IL2CPP/DefaultRuntimeMetadataAccess.cs:HiddenMethodInfo] | NULL vs metadata global |

**IL2CPP Runtime (libil2cpp):**

| Anchor | Region |
|---|---|
| [Source: il2cpp/libil2cpp/il2cpp-class-internals.h:MethodInfo-struct] | MethodInfo struct definition |
| [Source: il2cpp/libil2cpp/il2cpp-class-internals.h:VirtualInvokeData] | VirtualInvokeData struct |
| [Source: il2cpp/libil2cpp/il2cpp-class-internals.h:Il2CppRGCTXData] | RGCTX union definition |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:virtual-lookup] | Virtual/interface lookup functions |
| [Source: il2cpp/libil2cpp/codegen/il2cpp-codegen-il2cpp.h:rgctx-accessors] | RGCTX accessor functions |
| [Source: il2cpp/libil2cpp/vm/GlobalMetadata.cpp:InitializeRuntimeMetadata] | Metadata init implementation |
| [Source: il2cpp/libil2cpp/vm/GlobalMetadata.h:IsRuntimeMetadataInitialized] | Low-bit init check |
| [Source: il2cpp/libil2cpp/vm/GlobalMetadataFileInternals.h:encoded-token] | Encoded token format |

**Cross-References:**

| Reference | Consumer |
|---|---|
| Chapter 5 (Call Semantics) | Consumes C.3, C.4, C.5, C.7 for carrier identification at call sites |
| Annex A (Metadata Initialization) | Consumes C.6 for metadata slot lifecycle |
| Chapter 3 (Core Recovery Model) | Provides resolved/partial/unresolved vocabulary used throughout |

---

## Annex D: Source Anchor Index

### D.1 IL2CPP Codegen Sources (Unity.IL2CPP)

| File | Regions Referenced | Chapters |
|---|---|---|
| **MethodBodyWriter.cs** | call-emission, DirectCallFor, InvokerCallFor, WriteConstrainedCallExpressionFor, metadata-init-tracking, classesAlreadyInitializedInBlock, EnterTry, ExitTry, Code.Throw, Code.Rethrow, Code.Box, Code.Ldlen, field-access-emission | Ch 1, 4, 5, 6, Annex A |
| **MethodSignatureWriter.cs** | NeedsHiddenMethodInfo, FormatHiddenMethodArgument, ParametersForInternal | Ch 1, 5, Annex C |
| **SharedRuntimeMetadataAccess.cs** | metadata-access, rgctx-data-access, GetRGCTXAccess, HiddenMethodInfo, TypeInfoFor-with-reason | Ch 1, 3, 5, Annex A, C |
| **InterfaceAndVirtualInvokeWriter.cs** | WriteVirtual, WriteGenericVirtual, WriteInterface, WriteGenericInterface, consumption-patterns | Ch 5, Annex C |
| **DelegateMethodsWriter.cs** | delegate-invoke, delegate-stub-selection, delegate-ctor, delegate-fields | Ch 4, 5, Annex C |
| **DefaultRuntimeMetadataAccess.cs** | HiddenMethodInfo, FormatRuntimeIdentifier | Annex A, C |
| **CodeWriterExtensions.cs** | WriteMethodMetadataInitialization | Annex A |
| **ExceptionSupport.cs** | Prepare, MaxTryCatchDepth | Ch 6 |
| **RuntimeGenericContextInfo.cs** | RGCTX item kind enum | Annex C |

### D.2 IL2CPP Runtime Sources (libil2cpp)

| File | Regions Referenced | Chapters |
|---|---|---|
| **il2cpp-class-internals.h** | MethodInfo-struct, VirtualInvokeData, Il2CppRGCTXData | Annex C |
| **codegen/il2cpp-codegen-il2cpp.h** | init-metadata-functions, rgctx-accessors, virtual-lookup, null-check, IL2CPP_ARRAY_BOUNDS_CHECK, exception-macros, raise-functions | Ch 5, 6, 7, Annex A, C |
| **codegen/il2cpp-codegen-il2cpp.cpp** | Init metadata implementation | Annex A |
| **codegen/il2cpp-codegen-tiny.h** | bounds-check-debug-only, exception-macros | Ch 6, 7 |
| **vm/Runtime.cpp** | ClassInit, ClassInit-reentrant-detection | Ch 3, Annex A, B |
| **vm/Exception.cpp** | Raise, Rethrow, PrepareExceptionForThrow | Ch 6 |
| **vm/GlobalMetadata.cpp** | InitializeRuntimeMetadata | Annex A, C |
| **vm/GlobalMetadata.h** | IsRuntimeMetadataInitialized | Annex A, C |
| **vm/GlobalMetadataFileInternals.h** | encoded-token, Il2CppMetadataUsage | Annex A, C |
| **vm-utils/Finally.h** | FinallyHelper, destructor, fault-path | Ch 6 |
| **utils/ExceptionSupportStack.h** | ExceptionSupportStack | Ch 6 |

### D.3 Project Evidence Documents

| Document | Key Findings | Chapters |
|---|---|---|
| **ISIL_RETIREMENT** | active-route-decision, downstream-lowering-role, backend-infrastructure-remains-valuable | Ch 1, 10 |
| **WHY_PIVOT** | root-cause-substrate-erases-protocol-facts | Ch 1 |
| **LESSONS_LEARNED** | lesson-2 (conservative failure), lesson-4 (positional heuristics), lesson-6 (ground-truth), lesson-7 (source-owned progress), lesson-8 (recognized-but-fallback), lesson-9 (shim justification), lesson-11 (cross-arch), lesson-12 (fact preservation) | Ch 2, 3, 10, 11 |
| **DECISION_SUMMARY** | intended-architecture-shape, isil-demoted-to-backend-substrate | Ch 1, 10 |
| **GHIDRA_FEASIBILITY** | host-quality-boundary, metadata-priors-cannot-replace-call-site-evidence | Ch 2 |
| **FORMALIZATION_REQUIREMENTS** | backend-side-is-real-and-reusable | Ch 10 |
