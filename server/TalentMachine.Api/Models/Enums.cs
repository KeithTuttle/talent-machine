namespace TalentMachine.Api.Models;

public enum MembershipRole
{
    Owner,
    Member,
}

public enum Gender
{
    Male,
    Female,
    NonBinary,
}

public enum ConflictType
{
    OneOff,
    Weekly,
}

public enum AttendanceStatus
{
    Present,
    Absent,
    Excused,
}

public enum RehearsalType
{
    Music,
    Dance,
    Blocking,
    Runthrough,
    Other,
}

public enum StaffRole
{
    Director,
    Choreographer,
    MusicDirector,
    Producer,
}

/// <summary>How far along a number is in being taught. Null = not taught yet.</summary>
public enum TeachStatus
{
    Taught,
    NeedsReview,
    Complete,
}

/// <summary>Where a prop is in the gathering process. Default = Needed.</summary>
public enum PropStatus
{
    Needed,
    Sourced,
    Ready,
}

/// <summary>
/// Where a costume is in the gathering process (mirrors <see cref="PropStatus"/>).
/// Per-kid fitting is tracked separately on CostumeAssignment.IsFitted.
/// Default = Needed.
/// </summary>
public enum CostumeStatus
{
    Needed,
    Sourced,
    Ready,
}
