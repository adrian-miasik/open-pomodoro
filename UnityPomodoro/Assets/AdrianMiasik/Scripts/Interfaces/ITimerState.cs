using AdrianMiasik.ScriptableObjects;

namespace AdrianMiasik.Interfaces
{
    /// <summary>
    /// Any class that implements this interface will have to define a body method for StateUpdate().
    /// Then anytime the timer changes state such as transitioning from SETUP to RUNNING,
    /// all classes that implement this method will have their StateUpdate() method invoked.
    /// See enum <see cref="PomodoroTimer.States"/> for context.
    /// </summary>
    public interface ITimerState
    {
        /// <summary>
        /// Invoked when the timers current state changes.
        /// </summary>
        /// <param name="state">The new timer state</param>
        /// <param name="theme">The current theme</param>
        public void StateUpdate(PomodoroTimer.States state, Theme theme);
    }
}