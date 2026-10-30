using Swoovr.Contracts;
namespace Swoovr.Client;
public static class MessageText
{
    public const string RemovedKind="removed-by-moderation",RemovedBody="Removed by Swoovr.";
    /// <summary>A delayed page/send response cannot undo a removal already observed by this client.</summary>
    public static MessageView ApplyKnownRemoval(MessageView message,ISet<Guid> removed)
    {
        if(message.Kind==RemovedKind)removed.Add(message.Id);
        return removed.Contains(message.Id)?message with{Kind=RemovedKind,Text=RemovedBody,CanDelete=false,Deleted=false,Photo=null,PhotoState=null}:message;
    }
}
