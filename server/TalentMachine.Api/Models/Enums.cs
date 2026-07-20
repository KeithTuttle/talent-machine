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
