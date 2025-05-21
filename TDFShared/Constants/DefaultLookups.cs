using System.Collections.Generic;
using TDFShared.DTOs.Common;

namespace TDFShared.Constants
{
    public static class DefaultLookups
    {
        public static readonly List<LookupItem> DefaultDepartments = new()
        {
            new LookupItem("IT", "IT"),
            new LookupItem("HR", "HR"),
            new LookupItem("Finance", "Finance"),
            new LookupItem("Marketing", "Marketing"),
            new LookupItem("Operations", "Operations"),
            new LookupItem("Sales", "Sales")
        };

        public static readonly List<LookupItem> DefaultLeaveTypes = new()
        {
            new LookupItem("Annual", "Annual Leave"),
            new LookupItem("Casual", "Casual Leave"),
            new LookupItem("Sick", "Sick Leave"),
            new LookupItem("Permission", "Permission"),
            new LookupItem("WorkFromHome", "Work From Home"),
            new LookupItem("ExternalAssignment", "External Assignment")
        };
    }
}
