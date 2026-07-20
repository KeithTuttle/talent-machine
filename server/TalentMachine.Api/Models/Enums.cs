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

public enum CostumeGender
{
    All,
    Boys,
    Girls,
}
