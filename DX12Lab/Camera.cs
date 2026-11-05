using System;
using System.Numerics;

namespace DX12Lab;

public class Camera
{
    public Vector3 Position = new(0, 80, -220);
    public float Yaw = 0f;
    public float Pitch = -0.22f;

    public float MoveSpeed = 35f;
    public float LookSpeed = 0.003f;

    private int _lastMouseX, _lastMouseY;
    private bool _wasRightButton;

    public void Update(InputDevice input, float dt)
    {
        if (input.RightButton)
        {
            if (_wasRightButton)
            {
                Yaw -= (input.MouseX - _lastMouseX) * LookSpeed;
                Pitch += (input.MouseY - _lastMouseY) * LookSpeed;
                Pitch = Math.Clamp(Pitch, -MathF.PI / 2f + 0.01f, MathF.PI / 2f - 0.01f);
            }
            _lastMouseX = input.MouseX;
            _lastMouseY = input.MouseY;
        }
        _wasRightButton = input.RightButton;

        var forward = GetForward();
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

        float speed = MoveSpeed;
        if (input.IsKeyDown(0x10))
            speed *= 3.0f;

        if (input.IsKeyDown(0x57)) Position += forward * speed * dt;
        if (input.IsKeyDown(0x53)) Position -= forward * speed * dt;
        if (input.IsKeyDown(0x44)) Position += right * speed * dt;
        if (input.IsKeyDown(0x41)) Position -= right * speed * dt;
        if (input.IsKeyDown(0x20)) Position += Vector3.UnitY * speed * dt;
        if (input.IsKeyDown(0x11)) Position -= Vector3.UnitY * speed * dt;
    }

    private Vector3 GetForward() => new(
        MathF.Sin(Yaw) * MathF.Cos(Pitch),
        -MathF.Sin(Pitch),
        MathF.Cos(Yaw) * MathF.Cos(Pitch));

    public Vector3 Target => Position + GetForward();
}
