namespace CityGenPedestrianProbe
{
    public enum SignalChannel
    {
        State = 0,
        Proximity = 1,
        Transition = 2,
        Answer = 3,
        Alarm = 4,

        /// <summary>
        /// The body's own movement rather than anything around it.
        /// </summary>
        /// <remarks>
        /// The other four are all about the environment: what is nearby, what
        /// changed, what a query answered, what is dangerous. A footfall is
        /// about oneself, and there was no channel it belonged to. Appended
        /// rather than inserted so the numbers already written into session
        /// files keep meaning what they meant.
        /// </remarks>
        Locomotion = 5
    }
}
