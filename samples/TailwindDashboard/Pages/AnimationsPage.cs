using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using eQuantic.UI.Lottie;

namespace TailwindDashboard.Pages;

[Page("/animations")]
public class AnimationsPage : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new DashboardShell
        {
            Title = "Animations",
            Subtitle = "Lottie & dotLottie integrations",
            Children =
            {
                new Container
                {
                    ClassName = "grid grid-cols-1 md:grid-cols-2 gap-6 p-6",
                    Children =
                    {
                        new Card
                        {
                            Title = "Basic Animation",
                            Subtitle = "Simple loop from LottieFiles",
                            Children =
                            {
                                new LottiePlayer
                                {
                                    Src = "https://assets10.lottiefiles.com/packages/lf20_m6cu9zbe.json",
                                    Height = "300px",
                                    Autoplay = true,
                                    Loop = true
                                }
                            }
                        },
                        new Card
                        {
                            Title = "dotLottie Format",
                            Subtitle = "Efficient dotLottie file with controls",
                            Children =
                            {
                                new LottiePlayer
                                {
                                    Src = "https://lottie.host/6ad395f3-c5c7-4340-9602-0e980f769c0d/D3eS0I7M1P.lottie",
                                    Height = "300px",
                                    Autoplay = true,
                                    Loop = true,
                                    Controls = true
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
