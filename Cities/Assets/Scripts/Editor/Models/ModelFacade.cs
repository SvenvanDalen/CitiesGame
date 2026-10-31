using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>Assenstelsel van een gevel: <c>u</c> naar rechts gezien van buiten, <c>d</c> naar buiten.</summary>
    internal readonly struct Facade
    {
        public Facade(Vector3 origin, Vector3 right, Vector3 normal)
        {
            Origin = origin;
            Right = right;
            Normal = normal;
        }

        public Vector3 Origin { get; }

        public Vector3 Right { get; }

        public Vector3 Normal { get; }

        public Vector3 Point(float u, float y, float d) => Origin + (Right * u) + (Vector3.up * y) + (Normal * d);

        public Vector3[] Block(float u0, float u1, float y0, float y1, float d0, float d1) => new[]
        {
            Point(u0, y0, d0), Point(u1, y0, d0), Point(u1, y1, d0), Point(u0, y1, d0),
            Point(u0, y0, d1), Point(u1, y0, d1), Point(u1, y1, d1), Point(u0, y1, d1),
        };
    }
}
