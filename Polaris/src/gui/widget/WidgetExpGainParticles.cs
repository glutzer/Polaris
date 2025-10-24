using System;
using System.Collections.Generic;

namespace Polaris;

/// <summary>
/// Places an exp gain particle at the widget center.
/// </summary>
public class WidgetExpGainParticles : Widget
{
    private const float TEXT_SIZE = 64f;
    private readonly Queue<ExpGainParticle> particles = [];

    private class ExpGainParticle
    {
        public TextObject Text;
        public float Lifetime;
        public float Age;
        public Vector2 Position;
        public Vector2 Velocity;
        public bool IsAlive => Age < Lifetime;

        public ExpGainParticle(string text, Vector3 color, float lifetime, Vector2 position, Vector2 velocity, float scale)
        {
            Text = new TextObject(text, PolarisGuiThemes.Font, scale, new Vector4(color.X, color.Y, color.Z, 1f))
            {
                Shadow = true
            };
            Lifetime = lifetime;
            Position = position;
            Velocity = velocity;
        }

        public void Tick(float dt)
        {
            Age += dt;
            float ratio = Age / Lifetime;
            Position += Velocity * dt;

            Vector4 oldColor = Text.color;
            oldColor.W = 1f - ratio;
            Text.color = oldColor;
        }
    }

    public WidgetExpGainParticles(Widget? parent, Gui gui) : base(parent, gui)
    {
        Polaris.Instance(MainAPI.Capi).OnClientExperienceGain += OnExpGain;
    }

    private void OnExpGain(Constellation constellation, float amount, int currentLevel)
    {
        // Round amount to 2 digits.
        string text = $"+{MathF.Round(amount, 2)} {constellation.Name}";

        Vector2 position = new(XCenter, YCenter);

        // Velocity is between 20 and 40 on y, 5 and 15 on x.
        float xVelocity = -5f + (Random.Shared.NextSingle() * 10f);
        float yVelocity = -20f + (Random.Shared.NextSingle() * -20f);
        Vector2 velocity = new(xVelocity, yVelocity);

        float expToNextLevel = constellation.GetExpToReachLevel(currentLevel + 1);
        float ratio = Math.Clamp(amount / expToNextLevel, 0.05f, 1f);
        float size = TEXT_SIZE * ratio * MainAPI.GuiScale;

        ExpGainParticle particle = new(text, constellation.Color.Xyz, 2f, position, velocity, size);

        particles.Enqueue(particle);
    }

    private void TickParticles(float dt)
    {
        int count = particles.Count;
        for (int i = 0; i < count; i++)
        {
            ExpGainParticle particle = particles.Dequeue();
            particle.Tick(dt);
            if (particle.IsAlive)
            {
                particles.Enqueue(particle);
            }
        }
    }

    public override void OnRender(float dt, NuttyShader shader)
    {
        TickParticles(dt);
        foreach (ExpGainParticle particle in particles)
        {
            particle.Text.RenderCenteredLine(particle.Position.X, particle.Position.Y, shader, true);
        }
    }

    public override void Dispose()
    {
        if (MainAPI.Capi == null) return;
        Polaris.Instance(MainAPI.Capi).OnClientExperienceGain -= OnExpGain;
    }
}