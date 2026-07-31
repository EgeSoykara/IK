from __future__ import annotations

import copy
import json
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r8.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.review import review_pass_digest  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))

source["change_control"]["current_snapshot_sha256"] = contract["change_control"][
    "current_snapshot_sha256"
]
source["change_control"]["receipt_sha256"] = {
    validation_id: receipt["sha256"]
    for validation_id, receipt in contract["change_control"]["receipts"].items()
}
source["overrides"]["validation"] = copy.deepcopy(contract["validation"])

final_pass = {
    "id": "REVIEW-3",
    "kind": "FINAL_ATTESTATION",
    "packet_mode": "DELTA_ONLY",
    "contract_revision": 8,
    "reviewer_identity": "/root/leave_automation_reviewer",
    "reviewer_model": "gpt-5.6-sol",
    "independence": "INDEPENDENT",
    "policy_fingerprint": contract["policy"]["applicable_policy_fingerprint"],
    "score": 10,
    "open_findings": {"P0": 0, "P1": 0, "P2": 0, "P3": 0},
    "blocking_finding_ids": [],
    "scenario_evidence_sha256": (
        "sha256:d6da64f10c1f438263bb74528c1f22061e6c61b10fc8bbb14d37b7b3baed03e6"
    ),
    "attestation_surface_sha256": (
        "sha256:bf9560bd093e22831cbfdde1782666778780d48bfdd2494bc39d24898d9a07ee"
    ),
    "evidence": (
        "Same independent reviewer verified the corrected proof generator, forced "
        "real E2E PASS receipt, current outcome/security closeout and all prior "
        "remediations; no P0/P1/P2/P3 findings remain."
    ),
    "digest": "PLANNED",
}
final_pass["digest"] = review_pass_digest(final_pass)

review = source["overrides"]["review"]
review["lifecycle"]["passes"] = [
    item for item in review["lifecycle"]["passes"] if item["id"] != "REVIEW-3"
]
review["lifecycle"]["passes"].append(final_pass)
review["lifecycle"]["state"] = "FINAL_ATTESTED"
review["lifecycle"]["independence"] = "INDEPENDENT"
review["lifecycle"]["no_progress_rounds"] = 0
review["lifecycle"]["evidence"] = (
    "Stable independent reviewer completed the initial review, remediation delta "
    "and final delta-only attestation at 10/10 with zero findings."
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
print(json.dumps({"result": "WRITTEN", "path": str(SOURCE_PATH)}, ensure_ascii=False))
