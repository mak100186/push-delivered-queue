namespace PushDeliveredQueue.Core.Models;

public delegate Task<DeliveryResult> MessageHandler(MessageEnvelope message);