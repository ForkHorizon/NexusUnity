"""Lab errors. Callers must not treat these as successful measurements."""


class LabError(Exception):
    """Base class for laboratory failures."""


class UnsupportedMechanism(LabError):
    """The requested exposure mechanism is not actually supported.

    The lab refuses to silently substitute a different mechanism.
    """


class HoldoutLeak(LabError):
    """Search or mutation code tried to read holdout material."""


class SafetyError(LabError):
    """A process or project guard refused the operation."""


class RunVoid(LabError):
    """The run must not be scored. The model must not be started."""


class CandidateInvalid(LabError):
    """A candidate manifest failed schema or coverage validation."""
