"""Private type definitions for the NexusUnity Python bridge."""
from __future__ import annotations

from typing import Any, NotRequired, TypeAlias, TypedDict

JsonObject: TypeAlias = dict[str, Any]


class JsonRpcError(TypedDict):
    code: int
    message: str


class JsonRpcRequest(TypedDict):
    jsonrpc: str
    method: str
    params: JsonObject
    id: int


class ToolDefinition(TypedDict):
    name: str
    description: str
    inputSchema: JsonObject


class ResourceDefinition(TypedDict):
    uri: str
    name: str
    mimeType: NotRequired[str]


class JsonRpcResponse(TypedDict, total=False):
    result: JsonObject
    error: JsonRpcError


class TransformArguments(TypedDict, total=False):
    instance_id: int
    position: JsonObject
    rotation: JsonObject
    scale: JsonObject
    eulerAngles: JsonObject
    localScale: JsonObject


class WriteFileSpec(TypedDict):
    path: str
    content: str


class WriteError(TypedDict):
    path: str
    error: JsonRpcError


class WaitResultPayload(TypedDict):
    status: str
    time_waited_seconds: float


class TestResultsPayload(WaitResultPayload, total=False):
    timestamp_utc: str
    message: str
    result_path: str
    trigger: JsonObject


class WriteAndCompileSuccessPayload(WaitResultPayload):
    compiler_errors: list[JsonObject]


class WriteAndCompileFailurePayload(TypedDict):
    status: str
    message: str
    errors: list[WriteError]
