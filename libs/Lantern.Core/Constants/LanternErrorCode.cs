namespace Lantern.Core.Constants;

// Every failure a caller must see. ProblemExceptionHandler maps each to a status and a problem code; a new condition is a
// new member here, never a new exception class.
public enum LanternErrorCode
{
    AlreadyRegistered,
    CallerNotIdentified,
    ChildDeleting,
    ChildLimitReached,
    ChildNotFound,
    ClassNotAvailable,
    FamilyChanged,
    FamilyIdTaken,
    FamilyNotFound,
    InvalidRequest,
    NotRegistered,
}
