<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Local Indian Food Text/Vision SLM Training Guide

This guide describes a practical, privacy-conscious path to train and run a local
small language/multimodal model for Diet Dost. It is designed for Indian food
recognition, portion estimation, nutrition extraction, and meal-description
understanding. The application remains responsible for deterministic nutrition
math, clinical safety rules, authorization, and user confirmation; the model is
not a medical or dietetic authority.

> **Recommended architecture:** fine-tune a text SLM and a vision-language model
> separately, expose them through a local OpenAI-compatible runtime (Ollama
> during development, vLLM or another server for GPU deployment), and use
> Microsoft Agent Framework as the .NET orchestration/tool layer.

## 1. Decide what to train

Do not start by training one model to do everything. Split the problem into
measurable capabilities:

| Capability | Model | Training target |
|---|---|---|
| Meal photo understanding | Vision-language SLM | Dish/components, visible ingredients, portion cues, confidence, uncertainty |
| Text meal parsing | Text SLM | Indian/Indian-English descriptions to the same structured meal contract |
| Nutrition calculation | Existing .NET domain code | Calories and macros from a versioned food/composition database |
| Safety and clinical advice | Existing deterministic rules plus review | ICMR-NIN/WHO thresholds, conditions, medication warnings |

Start with supervised fine-tuning (SFT). Use preference training only after SFT
works and you have ranked alternatives. Reinforcement fine-tuning is appropriate
only when the task has a reliable, independently tested grader; it is not the
first choice for uncertain food-photo portion estimates.

## 2. Choose a local model and runtime

Use a model whose license, quantization, language coverage, and image support
are acceptable for the project. Candidate families change quickly, so pin and
record the exact model revision rather than relying on a moving tag.

- **Text SLM:** a 3B–8B instruction model with Hindi/English and, where
  required, regional-language coverage (for example Qwen, Gemma, or another
  permissively licensed model available to the team).
- **Vision SLM:** a 3B–8B vision-language model such as a current Qwen-VL or
  Gemma multimodal variant. Verify that the exact checkpoint supports image
  fine-tuning; a text-only checkpoint cannot learn visual features by adding
  image filenames to JSON.
- **Development runtime:** Ollama with a pinned model tag.
- **GPU serving/training:** vLLM, llama.cpp, or the runtime recommended by the
  model author. Use an OpenAI-compatible endpoint so the .NET integration does
  not depend on one serving product.

For local training, a CUDA GPU with 16–24 GB VRAM is a useful starting point for
QLoRA on a small text model. Vision fine-tuning can require substantially more
VRAM. If the local machine cannot train the chosen vision checkpoint, use
parameter-efficient training on a suitable GPU machine and keep inference local.

## 3. Establish the data contract before collecting data

Create a versioned contract under a training-data repository or an ignored local
folder, not inside production logs. Keep raw images separate from JSONL and
store a stable content hash for each image.

Every example should contain:

- `example_id`, `schema_version`, `source`, `license_or_consent`
- `image_id` or text input, but never a secret or unnecessary personal data
- `region`, `language`, `meal_context`, and `dish_family`
- a gold `answer` with components, portions, units, and uncertainty
- `evidence` (label, recipe, weighed portion, or expert annotation)
- `annotator_id`/review status without exposing a person's identity to the model

Use a normalized answer shape. The model proposes this object; the application
validates it and looks up nutrients:

```json
{
  "dish_name": "dal tadka",
  "region": "North Indian",
  "language": "en-IN",
  "items": [
    {
      "name": "toor dal",
      "preparation": "tadka",
      "quantity": 1,
      "unit": "katori",
      "estimated_grams": 180,
      "confidence": 0.78,
      "uncertainty": "oil quantity not visible"
    }
  ],
  "needs_user_confirmation": true
}
```

The response must not contain calories invented from the pixels. Resolve
`name + preparation + quantity` against the food database and calculate
calories, protein, carbohydrate, fat, fibre, sugar, and sodium in
`Nutrition.Domain`.

## 4. Build an Indian-focused dataset

### 4.0 Kaggle data: useful seed, not the complete training set

Kaggle data may be used as a seed, auxiliary pretraining set, or source of
visual diversity. It must not be accepted automatically as production training
data. Most food datasets provide a single dish-class label, while Diet Dost
needs components, household portions, uncertainty, and provenance.

Before importing any Kaggle dataset, create a dataset-audit record containing:

- dataset URL, version/download date, owner, and license text;
- whether commercial use, derivative datasets, and model-weight distribution
  are permitted;
- image count, file types, dimensions, corruption rate, and EXIF/face scan;
- duplicate and near-duplicate rate, including overlap with existing data;
- class, region, language, and dish-family distribution;
- label quality, annotation granularity, and known class ambiguity;
- whether images are suitable for the intended vision checkpoint;
- deletion/provenance procedure and the dataset hash.

Reject or quarantine the dataset if its license is unclear, derivative model
use is prohibited, images are not traceable, or its labels cannot be reviewed.
Do not put Kaggle files, credentials, or downloaded archives in the repository.
Keep the audit and a reproducible manifest; keep the raw data in an ignored
or controlled data store.

Use Kaggle labels primarily for visual recognition and augmentation. Add
human-reviewed examples for component decomposition, `katori`/`phulka`/`cup`
quantities, hidden-oil uncertainty, mixed dishes, regional variants, and
“ask the user” outcomes. Nutrition values must come from an approved food
composition source and the existing .NET calculation layer, not from a Kaggle
class label.

The first experiment should be deliberately small:

1. audit one candidate Kaggle dataset;
2. retain a reviewed subset with balanced Indian dish families;
3. add 300–1,000 reviewed text/vision examples;
4. reserve 30–100 independently collected, locked test examples;
5. benchmark the base model before fine-tuning;
6. train one QLoRA adapter and compare it with the base model;
7. stop if the adapter improves classification but worsens uncertainty,
   portion estimation, or safety behavior.

### 4.1 Coverage matrix

Before annotation, define target counts for:

- staples: rice, roti, phulka, paratha, dosa, idli, poha, upma, millet
- dals, legumes, vegetables, leafy greens, dairy, eggs, fish, and meat
- regional dishes: North, South, East, West, Northeast, and common diaspora
  preparations
- street food, packaged food, restaurant plating, home plating, tiffin, and
  thali combinations
- language and script: Indian English, Hinglish, Hindi, and the regional
  languages the product promises
- difficult cases: mixed dishes, occlusion, steel plates, low light, multiple
  servings, gravy/oil ambiguity, and visually similar dishes

Balance by dish family and region, not just by number of images. Split by
household, photographer, restaurant, and recipe source so near-duplicate
plates cannot leak into validation or test.

### 4.2 Annotation rules

Give annotators a short handbook and require a second review for uncertain
examples. Record:

1. visible components and preparation style;
2. a household measure (`katori`, `phulka`, `cup`, `piece`, `ladle`) and grams
   only when weighed or defensibly estimated;
3. oil/ghee, sugar, salt, and hidden ingredients as `unknown` when not observable;
4. a confidence score and an explicit uncertainty reason;
5. whether the correct behavior is to ask a follow-up question.

Do not label a precise portion from an image when the image cannot support it.
High-quality uncertainty labels teach the model to defer instead of hallucinate.

### 4.3 Privacy and provenance

Obtain consent or use data with a compatible license. Strip EXIF/GPS metadata,
remove faces and unrelated people, and delete names, phone numbers, addresses,
and medical notes from prompts. Keep a provenance manifest and a deletion
mechanism so a source can be removed and the dataset rebuilt.

## 5. Create train, validation, and test files

Use three disjoint sets, normally 80/10/10 for a first experiment. Keep the
test set locked and never use it to tune prompts or hyperparameters. Include
hard slices in every set, but ensure the same plate, recipe, or generated
paraphrase is not present in more than one split.

Text SFT JSONL:

```jsonl
{"messages":[{"role":"system","content":"Extract Indian meal components. Return only the agreed JSON schema. Do not invent hidden ingredients; mark uncertainty."},{"role":"user","content":"2 phulkas, one katori dal and cucumber salad for lunch."},{"role":"assistant","content":"{\"dish_name\":\"lunch thali\",\"items\":[{\"name\":\"phulka\",\"quantity\":2,\"unit\":\"piece\",\"confidence\":0.98},{\"name\":\"dal\",\"quantity\":1,\"unit\":\"katori\",\"confidence\":0.90},{\"name\":\"cucumber salad\",\"quantity\":1,\"unit\":\"serving\",\"confidence\":0.80}],\"needs_user_confirmation\":true}"}]}
```

Vision SFT uses an image content block in the user message. Use a local
base64/data pipeline or an image URI supported by the training system; do not
put an image in an assistant message:

```jsonl
{"messages":[{"role":"system","content":"Identify visible Indian meal components. Estimate only defensible portions and state uncertainty."},{"role":"user","content":[{"type":"text","text":"Identify the meal and return the JSON schema."},{"type":"image_url","image_url":{"url":"data:image/jpeg;base64,<generated-at-build-time>","detail":"high"}}]},{"role":"assistant","content":"{\"dish_name\":\"masala dosa\",\"items\":[{\"name\":\"masala dosa\",\"quantity\":1,\"unit\":\"piece\",\"confidence\":0.86},{\"name\":\"sambar\",\"quantity\":1,\"unit\":\"katori\",\"confidence\":0.62,\"uncertainty\":\"serving size partly occluded\"}],\"needs_user_confirmation\":true}"}]}
```

Validate every line, schema, image size/format, last-message role, and
duplicate hash before training. Keep generated synthetic data below the
human-reviewed data fraction, and never let synthetic examples enter the
locked test set.

## 6. Baseline before fine-tuning

Create a small gold test set (30–100 examples) covering easy, typical, and
hard cases. Run the base model and record:

- component exact/F1 accuracy;
- structured-output validity rate;
- portion error on weighed examples;
- calibrated confidence and abstention rate;
- follow-up-question precision;
- regional/language slice performance;
- latency, peak memory, and tokens per request.

The fine-tuned model must beat this baseline without degrading safety slices.
Use the same decoding settings and test set for every experiment.

## 7. Local text SLM training path (Python fallback)

Python is the practical training layer even when the product is .NET. Create a
reproducible environment and pin versions:

```powershell
py -3.11 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install torch transformers datasets trl peft accelerate bitsandbytes
```

Use QLoRA/LoRA first rather than full-parameter training. A typical experiment
is:

1. load the pinned instruction checkpoint with 4-bit quantization;
2. tokenize the SFT JSONL using the model's chat template;
3. train LoRA adapters with a low learning rate and early stopping;
4. evaluate the adapter on the locked validation set;
5. merge only for a release artifact after evaluation;
6. export a quantized model compatible with the selected runtime;
7. record model revision, dataset hash, hyperparameters, metrics, and license.

Start with 300–1,000 pristine examples rather than thousands of noisy
generations. Do not increase epochs to compensate for bad labels; fix the
dataset. For a second stage, use preference pairs only for style/abstention
behavior, not to override the nutrition database.

## 8. Local vision SLM training path

Use the same SFT objective, but train only on a vision-language checkpoint that
supports images. The process is:

1. resize/normalize images consistently while retaining food detail;
2. validate JPEG/PNG/WEBP, dimensions, and maximum size;
3. pair each image with a short task prompt and the reviewed JSON answer;
4. fine-tune the language/projector adapters with LoRA or QLoRA;
5. evaluate by dish family, region, lighting, plate type, and ambiguity;
6. test refusal/clarification behavior on deliberately unanswerable images;
7. export the adapter/checkpoint in the runtime's supported format.

Do not claim visual portion accuracy from text-only examples. Keep image
captioning, component detection, and portion estimation as separate metrics.
If a vision model cannot reliably estimate grams, let it emit household units
and uncertainty, then ask the user to correct the serving.

## 9. Optional Microsoft Foundry training path

Foundry is useful when local GPU capacity is insufficient or when you need a
managed experiment/deployment workflow. Use SFT first and validate the data
before upload:

```powershell
python scripts/validate/validate_sft.py training.jsonl
```

The Foundry fine-tuning format is chat-completions JSONL. Vision examples use
`image_url` content blocks in user messages. Establish the base-model baseline,
upload train/validation files, monitor the job, inspect checkpoints, deploy a
candidate, and compare it to the locked test set. Check current model
availability, image limits, pricing, region, and license in the official
documentation before committing to a model; these change independently of this
repository.

Foundry is an alternative training backend, not a replacement for the
application's .NET safety and nutrition layers. Keep a local export of dataset
manifests and evaluation results for reproducibility.

## 10. Run the trained model through Agent Framework in .NET

Install the current prerelease Agent Framework package and OllamaSharp version
from the official provider documentation:

```powershell
dotnet add package Microsoft.Agents.AI --prerelease
dotnet add package OllamaSharp
ollama pull <pinned-text-or-vision-model-tag>
```

The current Agent Framework Ollama integration constructs an `AIAgent` from an
`OllamaApiClient`. Keep the model name and endpoint in configuration, not in
source:

```csharp
using Microsoft.Agents.AI;
using OllamaSharp;

var endpoint = configuration["LocalAi:Endpoint"]
    ?? throw new InvalidOperationException("LocalAi:Endpoint is missing.");
var model = configuration["LocalAi:TextModel"]
    ?? throw new InvalidOperationException("LocalAi:TextModel is missing.");

AIAgent agent = new OllamaApiClient(new Uri(endpoint), model)
    .AsAIAgent(
        name: "IndianMealExtractor",
        instructions: """
            Extract visible Indian meal components into the application schema.
            Never invent hidden ingredients or exact grams. Mark uncertainty,
            use household units, and ask for confirmation when needed.
            Do not calculate clinical targets or diagnose a condition.
            """);
```

For images, send a `ChatMessage` containing both `TextContent` and
`UriContent`/data content, as described in the Agent Framework multimodal
documentation. The provider/model must support vision; a text-only Ollama model
will reject the image. Keep the existing `IFoodVisionAgent` boundary and add a
local implementation behind it rather than coupling controllers to Ollama.

Recommended flow:

```text
upload -> magic-byte/size/EXIF validation -> local vision agent
       -> JSON schema validation -> user review/correction
       -> deterministic food lookup and macro calculation
       -> clinical safeguards -> persistence and telemetry
```

Use Agent Framework tools for bounded actions such as food lookup and portion
normalization. Tools must validate arguments, enforce tenant/user scope, and
never allow the model to write clinical targets directly.

## 11. Configuration and fallback

Add a local-only configuration shape (use user secrets or environment variables
for secrets):

```json
{
  "LocalAi": {
    "Enabled": false,
    "Endpoint": "http://localhost:11434",
    "TextModel": "<pinned-text-model>",
    "VisionModel": "<pinned-vision-model>",
    "TimeoutSeconds": 30
  }
}
```

Use an explicit provider order, for example:

1. local vision model when enabled and healthy;
2. configured remote vision provider;
3. the existing offline clinical fallback;
4. a review state when confidence or schema validation is insufficient.

Do not silently turn a provider failure into a high-confidence answer. Log
provider/model/latency and validation outcome, but redact images, prompts
containing health data, and personal identifiers.

## 12. Evaluation and release gates

Add local-model cases to `tests/Nutrition.Vision.Evals` and retain the existing
clinical/domain tests. A candidate is releasable only when it meets agreed
thresholds on the locked set:

- valid JSON/schema: at least 99%;
- no fabricated hidden ingredient in the abstention slice;
- confidence calibration reviewed, not just raw accuracy;
- component F1 and portion error better than the baseline;
- no regression on regional-language or hard-lighting slices;
- clinical safety tests remain deterministic and green;
- p95 latency and memory fit the target device;
- model and dataset licenses are approved.

Run both application and model checks:

```powershell
dotnet test
dotnet build src/Nutrition.WebGateway/Nutrition.WebGateway.csproj
```

Store an experiment record for every candidate:

```text
model revision | adapter revision | dataset hash | split hash
hyperparameters | runtime/quantization | baseline metrics | candidate metrics
hardware | latency/memory | safety review | approval and rollback artifact
```

## 13. Suggested implementation order for Diet Dost

1. Freeze the response contract and move nutrient calculations behind the
   existing domain service.
2. Audit one Kaggle candidate and import only an approved, deduplicated subset.
3. Export de-identified correction records into reviewed text examples.
4. Build a 30–100 example locked test set and measure the current provider.
5. Train a text SLM adapter for description parsing and structured output.
6. Add the local Ollama Agent Framework adapter behind `IFoodVisionAgent`.
7. Train/evaluate a vision adapter only after the text contract and review UX
   are stable.
8. Add confidence calibration, abstention tests, and regional-language slices.
9. Enable local inference by configuration, observe it in shadow mode, then
   promote it only after it beats the baseline and passes the release gates.

## 14. Agent-ready implementation plan

Use the following work packages as independent, reviewable implementation
steps. An agent should complete one package, run its checks, and report the
files changed before starting the next package.

### Work package A - Contracts and configuration

**Objective:** Define the model boundary without changing the current provider
behavior.

1. Inspect `IFoodVisionAgent`, `IndianMealAnalysisResult`, food-item contracts,
   `MicrosoftAgentFoodVisionService`, and existing vision evaluation fixtures.
2. Add a versioned model-proposal DTO for components, household units,
   confidence, uncertainty, and `needs_user_confirmation`.
3. Add `LocalAi` options with `Enabled`, `Endpoint`, `TextModel`,
   `VisionModel`, and timeout settings. Bind and validate these options at
   startup; do not store credentials in source.
4. Add JSON-schema validation at the application boundary. Invalid output must
   produce a review/fallback result, never a successful high-confidence result.
5. Add unit tests for valid output, missing fields, invalid quantities,
   out-of-range confidence, unknown units, and malformed JSON.

**Acceptance criteria:** Existing remote and offline behavior is unchanged when
`LocalAi:Enabled` is false. Configuration errors are explicit. All new
contract tests pass.

### Work package B - Local Agent Framework provider

**Objective:** Add a local Ollama-backed implementation behind the existing
application interface.

1. Add the current compatible `Microsoft.Agents.AI` prerelease and
   `OllamaSharp` packages to the infrastructure project, after verifying the
   repository's target framework supports them.
2. Implement a `LocalOllamaFoodVisionService` or provider adapter using
   `OllamaApiClient` and `AsAIAgent`.
3. Keep system instructions narrowly scoped: extraction only, no clinical
   diagnosis, no invented grams, and explicit uncertainty.
4. For image requests, send text plus image content only when the configured
   model is multimodal. Select `VisionModel`, not `TextModel`.
5. Apply request timeout, cancellation, bounded response size, and structured
   output validation.
6. Add dependency injection registration and a feature-flagged provider
   decorator/factory. Do not couple controllers directly to Ollama.

**Acceptance criteria:** A local text request works against Ollama; an image
request is rejected clearly for a text-only model; cancellation and Ollama
unavailability reach the configured fallback; no image bytes or health data are
written to logs.

### Work package C - Deterministic nutrition and safety boundary

**Objective:** Ensure model output cannot bypass domain safeguards.

1. Map only validated food names, preparations, and household units to the
   existing food/composition lookup.
2. Calculate calories, macros, fibre, sugar, and sodium in the .NET domain
   layer, never from model-generated calorie values.
3. Reuse the existing confidence gate, user review flow, clinical calculators,
   medication warnings, and tenant authorization.
4. Treat unknown food, ambiguous portion, low confidence, or hidden
   ingredient as a confirmation request.
5. Add regression tests proving model output cannot write clinical targets or
   skip safety floors.

**Acceptance criteria:** The local model is advisory only. Every persisted meal
has validated items and deterministic nutrition values, or is explicitly held
for user review.

### Work package D - Training-data tooling

**Objective:** Make dataset creation reproducible without committing private
data.

1. Add a Kaggle/import audit template and require its approval before data
   enters `raw`.
2. Create an ignored local training-data layout with `raw`, `processed`,
   `train`, `validation`, `test`, and `manifests` folders.
3. Add a Python utility to normalize annotations into text and vision JSONL.
4. Add validators for schema, roles, image format/size, duplicate hashes,
   licenses/consent, split leakage, and required uncertainty fields.
5. Add deterministic train/validation/test splitting by source/household/image
   group, not random rows alone.
6. Add a dataset statistics report covering region, language, dish family,
   component frequency, and confidence distribution.
7. Keep the locked test set outside normal training commands and document how
   to rebuild it after a deletion request.

**Acceptance criteria:** A small checked-in synthetic fixture can be converted,
validated, split, and reported end-to-end. An unaudited or license-incompatible
Kaggle manifest is rejected. Real images and secrets remain ignored.

### Work package E - Local fine-tuning scripts

**Objective:** Provide repeatable text and vision QLoRA experiments.

1. Add a Python `pyproject.toml` or requirements file with pinned,
   compatible training dependencies.
2. Add a text SFT entry point using the selected model chat template.
3. Add a separate vision SFT entry point that fails fast for text-only
   checkpoints.
4. Support `--model`, `--train`, `--validation`, `--output`, `--seed`, and
   resource/quantization options.
5. Save adapter weights, tokenizer/configuration, dataset hashes, metrics, and
   the exact command line.
6. Add a local inference/evaluation command that emits structured metrics and
   sample failures.

**Acceptance criteria:** The scripts run on the documented fixture, are
restartable, and never overwrite the locked test set or silently continue with
invalid data.

### Work package F - Evaluation and shadow mode

**Objective:** Prove the local model is safe and useful before enabling it.

1. Extend `tests/Nutrition.Vision.Evals` with local-provider cases and fixed
   fixtures from the existing SDD.
2. Compare base, remote, and local candidates using the same locked test set.
3. Measure schema validity, component F1, portion error, abstention precision,
   confidence calibration, language/region slices, p95 latency, and memory.
4. Add prompt-injection, malicious text, blurry image, hidden-ingredient, and
   medication/clinical-boundary tests.
5. Add a shadow mode that invokes the local model but does not affect the
   user-visible or persisted result. Record only redacted comparison metrics.
6. Enable local-primary routing only after the release gates in this guide are
   met and a rollback switch is verified.

**Acceptance criteria:** Base-model results are recorded, local results beat
the agreed task thresholds without safety regression, and disabling the flag
immediately restores the prior provider path.

### Work package G - Documentation and operations

**Objective:** Make the system maintainable by another developer.

1. Document the supported model revisions, licenses, Ollama setup, GPU/CPU
   requirements, and expected latency.
2. Document dataset provenance, deletion/rebuild procedure, and model artifact
   retention.
3. Add a troubleshooting runbook for model-not-found, unsupported vision,
   timeout, invalid JSON, OOM, and fallback behavior.
4. Add a model card containing limitations: cuisine coverage, portion
   uncertainty, languages, known failure cases, and non-medical use.
5. Update the SDD traceability matrix and README when the implementation lands.

**Acceptance criteria:** A new developer can run the fixture, start Ollama,
invoke the local path, run evaluations, and disable the feature without
undocumented manual steps.

### Recommended agent execution order

Run **A**, then **B** and **C** together, then **D**, **E**, **F**, and **G**.
Do not begin model training until A and D are complete. Do not enable local
primary routing until F is complete. Keep each work package in a separate
commit or pull request when multiple agents are working in parallel.

### Ready-to-paste implementation prompt

```text
Implement the local Indian-food text/vision SLM integration described in
docs/LOCAL_INDIAN_FOOD_SLM_TRAINING_GUIDE.md.

Start with Work package A only. Inspect the existing contracts, provider,
dependency-injection registration, configuration, and vision evaluation tests.
Do not change current remote/offline behavior when LocalAi:Enabled is false.
Add the versioned model-proposal contract, validated LocalAi options, boundary
validation, and focused tests. Do not add real user data, API keys, or model
weights. Follow existing .NET patterns and run the smallest relevant tests.
Report changed files, tests run, and any dependency/version incompatibility.

Before Work package D, audit the supplied Kaggle dataset using the guide's
dataset-audit checklist. Do not download, commit, or train on it until its
license, provenance, duplicate rate, and annotation quality are recorded and
approved. Treat it as seed visual data only; add reviewed component/portion/
uncertainty labels and keep the locked test set independent.

After A is reviewed, implement B, then C, then the Python data/training
packages D/E, and finally evaluation/shadow mode F. Never let model-generated
calories or clinical targets bypass the deterministic domain layer.
```

### References

- [Microsoft Agent Framework overview](https://learn.microsoft.com/en-us/agent-framework/overview)
- [Agent Framework Ollama provider](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/ollama)
- [Agent Framework multimodal input](https://learn.microsoft.com/en-us/agent-framework/agents/multimodal)
- [Microsoft Foundry fine-tuning](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/fine-tuning)
- [ICMR-NIN resources](https://www.nin.res.in/)
