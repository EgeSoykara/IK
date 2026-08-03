from __future__ import annotations

import json
import sys
from pathlib import Path


ROOT = Path("/Users/egesoykara/Desktop/IK")
SKILL_ROOT = Path("/Users/egesoykara/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r11.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.review import review_pass_digest  # noqa: E402
from build_task_contract import build_contract  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
base_contract = contract_api.loads_json(
    (CONTRACT_DIR / "revisions/10.json").read_text(encoding="utf-8")
)

final_pass = {
    "id": "REVIEW-6",
    "kind": "FINAL_ATTESTATION",
    "packet_mode": "DELTA_ONLY",
    "contract_revision": 11,
    "reviewer_identity": "/root/leave_automation_reviewer",
    "reviewer_model": "gpt-5.6-sol",
    "independence": "INDEPENDENT",
    "policy_fingerprint": contract["policy"]["applicable_policy_fingerprint"],
    "score": 10,
    "open_findings": {"P0": 0, "P1": 0, "P2": 0, "P3": 0},
    "blocking_finding_ids": [],
    "scenario_evidence_sha256": "sha256:5a46d9a3e0e8b19e3faa8aa20ef2cfbc78bfb7ba561568d91a1d1cc91ac7f8fe",
    "attestation_surface_sha256": "sha256:a65485f5222c1b73cf24e31810d85b4ec81ff5eda20c4e816c7e70721d36d2d3",
    "evidence": (
        "Same independent reviewer verified all three revision-11 P1 remediations, current "
        "focused/full/build/EF/real-MSSQL-browser PASS receipts, aggregate outcome PASS, "
        "entity-mapping PASS and six-of-six security closeout; no P0/P1/P2/P3 findings "
        "or external blockers remain."
    ),
    "digest": "PLANNED",
}
final_pass["digest"] = review_pass_digest(final_pass)

review = source["overrides"]["review"]
review["lifecycle"]["passes"] = [
    item for item in review["lifecycle"]["passes"] if item["id"] != "REVIEW-6"
] + [final_pass]
review["lifecycle"]["state"] = "FINAL_ATTESTED"
review["lifecycle"]["independence"] = "INDEPENDENT"
review["lifecycle"]["no_progress_rounds"] = 0
review["lifecycle"]["evidence"] = (
    "Stable independent reviewer completed revision-11 delta review and final attestation "
    "at 10/10 with zero findings, bound to the current scenario and closeout surface."
)

source["checkpoint"] = {
    "phase": "FINAL_CLOSEOUT",
    "completed_step_ids": [
        "DISCOVERY",
        "IMPLEMENTATION",
        "VALIDATION",
        "REVIEW",
        "FINAL_CLOSEOUT",
    ],
    "pending_step_ids": [],
    "invalidated_validation_ids": [],
    "active_remediation_batch_id": None,
}

contract_api.atomic_write_json(SOURCE_PATH, source, compact=False)
compiled = build_contract(source, SKILL_ROOT, base_contract=base_contract)
errors = contract_api.validate_contract(
    compiled,
    skill_root=SKILL_ROOT,
    contract_path=CONTRACT_PATH,
    allow_blocked=False,
    final=False,
)
if errors:
    raise RuntimeError("Final-attested contract invalid:\n" + "\n".join(errors))
contract_api.atomic_write_json(CONTRACT_PATH, compiled, compact=False)
print(json.dumps({"result": "WRITTEN", "contract": str(CONTRACT_PATH)}, ensure_ascii=False))
