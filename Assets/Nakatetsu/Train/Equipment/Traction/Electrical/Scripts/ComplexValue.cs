using UnityEngine;

namespace Nakatetsu.Train.Equipment.Traction.Electrical
{
    public readonly struct ComplexValue
    {
        public ComplexValue(float real, float imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public float Real { get; }
        public float Imaginary { get; }
        public float Magnitude => Mathf.Sqrt(Real * Real + Imaginary * Imaginary);

        public static ComplexValue J => new(0f, 1f);

        public static implicit operator ComplexValue(float real) =>
            new(real, 0f);

        public static ComplexValue operator +(ComplexValue left, ComplexValue right) =>
            new(left.Real + right.Real, left.Imaginary + right.Imaginary);

        public static ComplexValue operator *(ComplexValue left, ComplexValue right) =>
            new(
                left.Real * right.Real - left.Imaginary * right.Imaginary,
                left.Real * right.Imaginary + left.Imaginary * right.Real);

        public static ComplexValue operator /(ComplexValue left, ComplexValue right)
        {
            float denominator = right.Real * right.Real + right.Imaginary * right.Imaginary;
            if (denominator <= float.Epsilon)
            {
                return default;
            }

            return new ComplexValue(
                (left.Real * right.Real + left.Imaginary * right.Imaginary) / denominator,
                (left.Imaginary * right.Real - left.Real * right.Imaginary) / denominator);
        }

        public static ComplexValue operator /(float left, ComplexValue right) =>
            new ComplexValue(left, 0f) / right;
    }
}
