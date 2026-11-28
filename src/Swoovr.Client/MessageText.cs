using Swoovr.Contracts;
namespace Swoovr.Client;
public static class MessageText
{
    public const string RemovedKind="removed-by-moderation",RemovedBody="Removed by Swoovr.";
    /// <summary>
    /// #300 (D380): what the sender reads once they reached the other person's unanswered-message limit, on the web and in
    /// the app. Neutral: it never says the number and never points to another way of reaching them.
    /// </summary>
    public const string AwaitingReply="You've reached the unanswered message limit for this conversation. You can send more when they reply.";
    /// <summary>#300: the recipient's own setting, in Notifications next to "Accept photos in chat".</summary>
    public const string LimitLabel="Limit unanswered messages",
        LimitHelp="The most messages someone can send you in a row without a reply, in each chat. When they reach it, they can write again once you reply. Time passing doesn't reset it, and they aren't told your number.";
    /// <summary>#300: the choices, 0 for Off (the default).</summary>
    public static readonly IReadOnlyList<(int Value,string Label)> LimitChoices=[(0,"Off"),(3,"3 messages"),(5,"5 messages"),(10,"10 messages")];
    /// <summary>#300: a message from the other member that ends the sender's wait (a reply: text, photo or meet-up, never a system event or farewell).</summary>
    public static bool IsReply(MessageView message)=>!message.Mine&&message.Kind is "text" or "photo" or "meetup" or "location";
    /// <summary>A delayed page/send response cannot undo a removal already observed by this client.</summary>
    public static MessageView ApplyKnownRemoval(MessageView message,ISet<Guid> removed)
    {
        // PR 583 review: a meet-up whose note staff removed arrives as its card with the note "Removed by Swoovr.".
        if(message.Kind==RemovedKind||message.Meetup is{Note:RemovedBody})removed.Add(message.Id);
        if(!removed.Contains(message.Id))return message;
        // The card and its controls stay (the plan can still be answered); a delayed response can't bring the note back.
        return message.Meetup is{}plan?message with{Text=RemovedBody,CanDelete=false,Meetup=plan with{Note=RemovedBody}}
            :message with{Kind=RemovedKind,Text=RemovedBody,CanDelete=false,Deleted=false,Photo=null,PhotoState=null};
    }
}
