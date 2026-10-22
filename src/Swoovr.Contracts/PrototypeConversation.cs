namespace Swoovr.Contracts;

// Disposable fictional fixtures only. Actor is a simulation selector, never authentication.
public sealed record PrototypeConversationCreate(string Travel);
public sealed record PrototypeConversationAction(string Actor,string Action,long Revision);
public sealed record PrototypeConversationMessage(string Sender,string Text);
public sealed record PrototypeConversation(bool IsSimulation,Guid Id,long Revision,string State,
    string Actor,bool CanRead,bool CanReport,IReadOnlyList<PrototypeConversationMessage> Messages);
