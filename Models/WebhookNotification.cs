namespace DidiWebhookReceiver.Models;

public class WebhookNotification
{
    public int Id { get; set; }
    public string MovementType { get; set; } = default!;
    public string IdTransaction { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;

    // Campos opcionales adicionales según el JSON de DIDI
    public string? Description { get; set; }
    public DateTime? MovementDate { get; set; }
}
