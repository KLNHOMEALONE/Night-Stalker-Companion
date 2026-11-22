using NightStalkerCompanion.ViewModels;

namespace NightStalkerCompanion.Views;

public partial class ReticlePage : ContentPage
{
    private readonly ReticleViewModel viewModel;

    public ReticlePage(ReticleViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = viewModel.AppearingAsync();
    }

    /// <summary>
    /// Enter в поле смещения = «записать». Текст забирается из поля прямо здесь, а не
    /// через привязку: двусторонняя привязка Entry отдаёт значение только при потере
    /// фокуса, а Enter фокус не забирает, и на команду ушёл бы старый текст. Ось поля
    /// приходит в StyleId, их две — X и Y.
    /// </summary>
    private void OnOffsetCompleted(object? sender, EventArgs e)
    {
        if (sender is not Entry { StyleId: { Length: > 0 } axis } entry)
            return;

        if (axis == "X")
            viewModel.XInput = entry.Text;
        else
            viewModel.YInput = entry.Text;

        viewModel.ApplyOffsetCommand.Execute(axis);
    }
}
