using System;
using System.Collections.Generic;

namespace Calendar
{
    // The following types define the structure of an object of type CalendarActions that represents a list of requested calendar actions
    public class CalendarActions
    {
        public List<Action> Actions { get; set; }
    }

    // Base abstract class for different action types
    public abstract class Action { }

    // Represents the "add event" action
    public class AddEventAction : Action
    {
        public string ActionType { get; set; } = "add event";
        public Event Event { get; set; }
    }

    // Represents the "remove event" action
    public class RemoveEventAction : Action
    {
        public string ActionType { get; set; } = "remove event";
        public EventReference EventReference { get; set; }
    }

    // Represents the "add participants" action
    public class AddParticipantsAction : Action
    {
        public string ActionType { get; set; } = "add participants";
        public EventReference EventReference { get; set; }
        public List<string> Participants { get; set; }
    }

    // Represents the "change time range" action
    public class ChangeTimeRangeAction : Action
    {
        public string ActionType { get; set; } = "change time range";
        public EventReference EventReference { get; set; }
        public EventTimeRange TimeRange { get; set; }
    }

    // Represents the "change description" action
    public class ChangeDescriptionAction : Action
    {
        public string ActionType { get; set; } = "change description";
        public EventReference EventReference { get; set; }
        public string Description { get; set; }
    }

    // Represents the "find events" action
    public class FindEventsAction : Action
    {
        public string ActionType { get; set; } = "find events";
        public EventReference EventReference { get; set; }
    }

    // Represents the action for when user input is not understood
    public class UnknownAction : Action
    {
        public string ActionType { get; set; } = "unknown";
        public string Text { get; set; }
    }

    // Represents the time range of an event
    public class EventTimeRange
    {
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Duration { get; set; }
    }

    // Represents an event
    public class Event
    {
        public string Day { get; set; }
        public EventTimeRange TimeRange { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public List<string> Participants { get; set; }
    }

    // Represents the properties used by the requester in referring to an event
    public class EventReference
    {
        public string Day { get; set; }
        public string DayRange { get; set; }
        public EventTimeRange TimeRange { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public List<string> Participants { get; set; }
    }
}
