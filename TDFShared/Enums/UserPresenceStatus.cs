namespace TDFShared.Enums
{
    /// <summary>
    /// Represents the online presence status of a user in the system
    /// </summary>
    public enum UserPresenceStatus
    {
        /// <summary>
        /// User is not connected to the system
        /// </summary>
        Offline = 0,
        
        /// <summary>
        /// User is connected and active in the system
        /// </summary>
        Online = 1,
        
        /// <summary>
        /// User is connected but has been inactive for a period of time
        /// </summary>
        Away = 2,
        
        /// <summary>
        /// User is connected but has set their status to indicate they are busy
        /// </summary>
        Busy = 3,
        
        /// <summary>
        /// User is connected but has requested not to be disturbed
        /// </summary>
        DoNotDisturb = 4,
        
        /// <summary>
        /// User is temporarily away but will return shortly
        /// </summary>
        BeRightBack = 5,
        
        /// <summary>
        /// User is online but appears offline to others
        /// </summary>
        AppearingOffline = 6
    }
} 