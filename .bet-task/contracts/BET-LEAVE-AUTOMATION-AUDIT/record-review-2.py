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

review_pass = {
    "id": "REVIEW-2",
    "kind": "DELTA_REVIEW",
    "packet_mode": "DELTA_ONLY",
    "contract_revision": 8,
    "reviewer_identity": "/root/leave_automation_reviewer",
    "reviewer_model": "gpt-5.6-sol",
    "independence": "INDEPENDENT",
    "policy_fingerprint": contract["policy"]["applicable_policy_fingerprint"],
    "score": 9,
    "open_findings": {"P0": 0, "P1": 1, "P2": 0, "P3": 0},
    "blocking_finding_ids": ["P1-E2E-PROOF-BINDING"],
    "scenario_evidence_sha256": (
        "sha256:83d9d9943740350e5d02eed1a5ca2fc09b159bfae2fc5a8ef7dd0109af0dcd71"
    ),
    "attestation_surface_sha256": (
        "sha256:797137474895604505a4d58865ac53e54ec5847b9cf1a89a47f0ee2f9b1a2b96"
    ),
    "evidence": (
        "Same reviewer resolved the five REVIEW-1 implementation findings and found "
        "one invalid E2E proof binding: the generator used a stale scenario id and "
        "non-canonical observed-layer names."
    ),
    "digest": "PLANNED",
}
review_pass["digest"] = review_pass_digest(review_pass)

review = source["overrides"]["review"]
review["lifecycle"]["passes"] = [
    item for item in review["lifecycle"]["passes"] if item["id"] != "REVIEW-2"
]
review["lifecycle"]["passes"].append(review_pass)
review["lifecycle"]["state"] = "DELTA_REVIEW_IN_PROGRESS"
review["lifecycle"]["no_progress_rounds"] = 0
review["lifecycle"]["evidence"] = (
    "REVIEW-2 implementation findings are resolved; its sole proof-binding finding "
    "was remediated by the generator and a forced real E2E rerun."
)

source["overrides"]["validation"]["remediation"]["batches"] = [
    item
    for item in source["overrides"]["validation"]["remediation"]["batches"]
    if item["id"] != "REVIEW-REMEDIATION-R8"
]

contract_api.atomic_write_json(SOURCE_PATH, source, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(SOURCE_PATH)}, ensure_ascii=False))
