namespace PushDeliveredQueue.Core.Models;

public enum PostMessageFailedBehavior
{
    RetryOnceThenCommit, // will retry once immediately and then commit the message
    RetryOnceThenDLQ, // will retry once immediately and then commit the message and add to DLQ
    AddToDLQ, // will add to DLQ directly
    Commit, // will commit and move on
    Block
}
