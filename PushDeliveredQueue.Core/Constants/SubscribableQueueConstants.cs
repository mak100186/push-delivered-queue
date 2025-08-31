namespace PushDeliveredQueue.Core.Constants;

public static class SubscribableQueueConstants
{
    // Context items
    public const string ContextItemSubscriberId = "SubscriberId";
    public const string ContextItemCursorState = "CursorState";
    public const string ContextItemMessage = "Message";

    // Log messages - Message operations
    public const string LogMessagePayloadChanged = "Message {MessageId} payload changed";
    public const string LogMessageNotFoundForPayloadChange = "Message {MessageId} not found for payload change";
    public const string LogMessageNotFoundInDlqForReplay = "Message {MessageId} not found in DLQ for replay by subscriber {SubscriberId}";
    public const string LogMessageNotFoundInBufferForReplay = "Message {MessageId} not found in buffer for replay by subscriber {SubscriberId}";

    // Log messages - Subscriber operations
    public const string LogNonExistentSubscriberForDlqReplay = "Attempted to replay DLQ for non-existent subscriber {SubscriberId}";
    public const string LogNonExistentSubscriberForReplay = "Attempted to replay for non-existent subscriber {SubscriberId}";
    public const string LogSubscriberReplayedDlqMessage = "Replaying message {MessageId} to subscriber {SubscriberId} from dead letter queue";
    public const string LogSubscriberProcessedDlqMessage = "Subscriber {SubscriberId} successfully processed DLQ message {MessageId}";
    public const string LogSubscriberDlqProcessingFailed = "DLQ Message processing failed. Not removing from DLQ {SubscriberId} {@Message}";
    public const string LogSubscriberReplayedAllDlqMessages = "Subscriber {SubscriberId} replayed all DLQ messages";
    public const string LogSubscriberNoDlqMessagesToReplay = "Subscriber {SubscriberId} has no messages in DLQ to replay";
    public const string LogSubscriberReplayedFromMessage = "Subscriber {SubscriberId} replayed from message {MessageId} at index {Index}";

    // Log messages - Subscriber state validation
    public const string LogSubscriberNotStartedConsuming = "Subscriber {SubscriberId} has not started consuming messages yet. Cannot replay.";
    public const string LogSubscriberHasUncommittedMessages = "Subscriber {SubscriberId} has uncommitted messages. Cannot replay.";
    public const string LogSubscriberAtMiddleOfBuffer = "Subscriber {SubscriberId} is at the middle of the buffer. Cannot replay.";

    // Log messages - Message delivery
    public const string LogMessageDeliveryFailed = "Message delivery failed. Invoking OnMessageFailedHandler. {SubscriberId} {@Message}";
    public const string LogOnMessageFailedHandlerReturned = "OnMessageFailedHandler returned {PostMessageFailedBehavior} for {SubscriberId} {@Message}";
    public const string LogSubscriberAddedMessageToDlq = "Subscriber {SubscriberId} added message {MessageId} to DLQ";
    public const string LogAttemptedAddToDlqForNonExistentSubscriber = "Attempted to add to DLQ for non-existent subscriber {SubscriberId}";
    public const string LogSubscriberCommittedMessage = "Subscriber {SubscriberId} committed message at index {Index}";
    public const string LogAttemptedCommitForNonExistentSubscriber = "Attempted to commit for non-existent subscriber {SubscriberId}";
    public const string LogDispatchingMessageToSubscriber = "Dispatching message {MessageId} to subscriber {SubscriberId} at index {Index}";

    // Log messages - System operations
    public const string LogErrorDuringPruningExpiredMessages = "Error during pruning expired messages.";

    // Configuration section name
    public const string ConfigurationSectionName = "SubscribableQueue";

    // Message envelope format
    public const string MessageEnvelopeToStringFormat = "MessageEnvelope(Id={0}, CreatedAt={1}, Payload={2})";
}
