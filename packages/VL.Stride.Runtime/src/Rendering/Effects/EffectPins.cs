using System;
using System.Collections.Generic;
using System.Reflection;
using VL.Core;
using Stride.Rendering;
using Stride.Core.Mathematics;
using Stride.Graphics;
using System.Runtime.CompilerServices;

namespace VL.Stride.Rendering
{
    abstract class EffectPinDescription : IVLPinDescription, IInfo, IVLPinDescriptionWithVisibility
    {
        public abstract string Name { get; }
        public abstract Type Type { get; }
        public abstract object DefaultValueBoxed { get; }

        public string Summary { get; set; }
        public string Remarks { get; set; }

        public bool IsVisible { get; set; } = true;

        object IVLPinDescription.DefaultValue => DefaultValueBoxed;

        public abstract IVLPin CreatePin(GraphicsDevice graphicsDevice, ParameterCollection parameters);
    }

    class PinDescription<T> : EffectPinDescription
    {
        public PinDescription(string name, T defaultValue = default(T))
        {
            Name = name;
            DefaultValue = defaultValue;
        }

        public override string Name { get; }
        public override Type Type => typeof(T);
        public override object DefaultValueBoxed => DefaultValue;
        public T DefaultValue { get; }

        public override IVLPin CreatePin(GraphicsDevice graphicsDevice, ParameterCollection parameters) => new Pin<T>(Name, DefaultValue);
    }

    class ParameterPinDescription : EffectPinDescription
    {
        public readonly ParameterKey Key;
        public readonly int Count;
        public readonly bool IsPermutationKey;

        public ParameterPinDescription(HashSet<string> usedNames, ParameterKey key, int count = 1, object defaultValue = null, bool isPermutationKey = false, string name = null, Type typeInPatch = null)
        {
            Key = key;
            IsPermutationKey = isPermutationKey;
            Count = count;
            Name = name ?? key.GetPinName(usedNames);
            var elementType = typeInPatch ?? key.PropertyType;
            defaultValue = defaultValue ?? key.DefaultValueMetadata?.GetDefaultValue();
            // TODO: This should be fixed in Stride
            if (key.PropertyType == typeof(Matrix))
                defaultValue = Matrix.Identity;
            if (count > 1)
            {
                Type = elementType.MakeArrayType();
                var arr = Array.CreateInstance(elementType, count);
                for (int i = 0; i < arr.Length; i++)
                    arr.SetValue(defaultValue, i);
                DefaultValueBoxed = arr;
            }
            else
            {
                Type = elementType;
                DefaultValueBoxed = defaultValue;
            }
        }

        public override string Name { get; }
        public override Type Type { get; }
        public override object DefaultValueBoxed { get; }
        public override IVLPin CreatePin(GraphicsDevice graphicsDevice, ParameterCollection parameters) => EffectPins.CreatePin(graphicsDevice, parameters, Key, Count, IsPermutationKey, DefaultValueBoxed, Type);
    }

    static class EffectPins
    {
        public static IVLPin CreatePin(GraphicsDevice graphicsDevice, ParameterCollection parameters, ParameterKey key, int count, bool isPermutationKey, object value, Type type)
        {
            if (key is ValueParameterKey<Color4> colorKey)
                return new ColorParameterPin(parameters, colorKey, graphicsDevice.ColorSpace, (Color4)value);

            var argument = key.GetType().GetGenericArguments()[0];

            if (type.IsEnum)
            {
                var createPinMethod = typeof(EffectPins).GetMethod(nameof(CreateEnumPin), BindingFlags.Static | BindingFlags.Public);
                return createPinMethod.MakeGenericMethod(argument, type).Invoke(null, new object[] { parameters, key, value }) as IVLPin;
            }

            if (isPermutationKey)
            {
                var createPinMethod = typeof(EffectPins).GetMethod(nameof(CreatePermutationPin), BindingFlags.Static | BindingFlags.Public);
                return createPinMethod.MakeGenericMethod(argument).Invoke(null, new object[] { parameters, key, value }) as IVLPin;
            }
            else if (argument.IsValueType)
            {
                if (count > 1)
                {
                    var createPinMethod = typeof(EffectPins).GetMethod(nameof(CreateArrayPin), BindingFlags.Static | BindingFlags.Public);
                    return createPinMethod.MakeGenericMethod(argument).Invoke(null, new object[] { parameters, key, value }) as IVLPin;
                }
                else
                {
                    var createPinMethod = typeof(EffectPins).GetMethod(nameof(CreateValuePin), BindingFlags.Static | BindingFlags.Public);
                    return createPinMethod.MakeGenericMethod(argument).Invoke(null, new object[] { parameters, key, value }) as IVLPin;
                }
            }
            else
            {
                var createPinMethod = typeof(EffectPins).GetMethod(nameof(CreateResourcePin), BindingFlags.Static | BindingFlags.Public);
                return createPinMethod.MakeGenericMethod(argument).Invoke(null, new object[] { parameters, key }) as IVLPin;
            }
        }

        public static IVLPin CreatePermutationPin<T>(ParameterCollection parameters, PermutationParameterKey<T> key, T value)
        {
            return new PermutationParameterPin<T>(parameters, key, value);
        }

        public static IVLPin CreateEnumPin<T, TEnum>(ParameterCollection parameters, ValueParameterKey<T> key, TEnum value) where T : unmanaged where TEnum : unmanaged
        {
            return new EnumParameterPin<T, TEnum>(parameters, key, value);
        }

        public static IVLPin CreateValuePin<T>(ParameterCollection parameters, ValueParameterKey<T> key, T value) where T : struct
        {
            return new ValueParameterPin<T>(parameters, key, value);
        }

        public static IVLPin CreateArrayPin<T>(ParameterCollection parameters, ValueParameterKey<T> key, T[] value) where T : struct
        {
            return new ArrayValueParameterPin<T>(parameters, key, value);
        }

        public static IVLPin CreateResourcePin<T>(ParameterCollection parameters, ObjectParameterKey<T> key) where T : class
        {
            return new ResourceParameterPin<T>(parameters, key);
        }
    }

    abstract class ParameterPin
    {
        public readonly ParameterCollection Parameters;
        public readonly ParameterKey ParameterKey;

        public ParameterPin(ParameterCollection parameters, ParameterKey key)
        {
            Parameters = parameters;
            ParameterKey = key;
        }
    }

    abstract class PermutationParameterPin : ParameterPin
    {
        public PermutationParameterPin(ParameterCollection parameters, ParameterKey key) 
            : base(parameters, key)
        {
        }

        public bool HasChanged { get; protected set; }
    }

    class PermutationParameterPin<T> : PermutationParameterPin, IVLPin<T>
    {
        public readonly PermutationParameterKey<T> Key;
        readonly EqualityComparer<T> comparer = EqualityComparer<T>.Default;

        public PermutationParameterPin(ParameterCollection parameters, PermutationParameterKey<T> key, T value) 
            : base(parameters, key)
        {
            this.Key = key;
            this.Value = value;
        }

        public T Value
        {
            get => Parameters.Get(Key);
            set
            {
                HasChanged = !comparer.Equals(value, Value);
                Parameters.Set(Key, value);
            }
        }

        object IVLPin.Value
        {
            get => Value;
            set => Value = (T)value;
        }
    }

    class ValueParameterPin<T> : ParameterPin, IVLPin<T> where T : struct
    {
        public readonly ValueParameterKey<T> Key;

        public ValueParameterPin(ParameterCollection parameters, ValueParameterKey<T> key, T value)
            : base(parameters, key)
        {
            this.Key = key;
            this.Value = value;
        }

        public T Value
        {
            get => Parameters.Get(Key);
            set => Parameters.Set(Key, value);
        }

        object IVLPin.Value
        {
            get => Value;
            set => Value = (T)value;
        }
    }

    class EnumParameterPin<T, TEnum> : ParameterPin, IVLPin<TEnum> where T : unmanaged where TEnum : unmanaged
    {
        public readonly ValueParameterKey<T> Key;

        public EnumParameterPin(ParameterCollection parameters, ValueParameterKey<T> key, TEnum value)
            : base(parameters, key)
        {
            this.Key = key;
            this.Value = value;
        }

        public TEnum Value
        {
            get
            {
                T val = Parameters.Get(Key);
                return Unsafe.As<T, TEnum>(ref val);
            }
            set => Parameters.Set(Key, Unsafe.As<TEnum, T>(ref value));
        }

        object IVLPin.Value
        {
            get => Value;
            set => Value = (TEnum)value;
        }
    }

    class ColorParameterPin : ParameterPin, IVLPin<Color4>
    {
        public readonly ValueParameterKey<Color4> Key;
        public readonly ColorSpace ColorSpace;

        public ColorParameterPin(ParameterCollection parameters, ValueParameterKey<Color4> key, ColorSpace colorSpace, Color4 value)
            : base(parameters, key)
        {
            this.Key = key;
            this.ColorSpace = colorSpace;
            this.Value = value;
        }

        public Color4 Value
        {
            get => Parameters.Get(Key);
            set => Parameters.Set(Key, value.ToColorSpace(ColorSpace));
        }

        object IVLPin.Value
        {
            get => Value;
            set => Value = (Color4)value;
        }
    }

    class ArrayValueParameterPin<T> : ParameterPin, IVLPin<T[]> where T : struct
    {
        public readonly ValueParameterKey<T> Key;

        public ArrayValueParameterPin(ParameterCollection parameters, ValueParameterKey<T> key, T[] value) 
            : base(parameters, key)
        {
            this.Key = key;
            this.Value = value;
        }

        // TODO: Add overloads to Stride wich take accessor instead of less optimal key
        public T[] Value
        {
            get => Parameters.GetValues(Key);
            set
            {
                if (value.Length > 0)
                    Parameters.Set(Key, value);
            }
        }

        object IVLPin.Value
        {
            get => Value;
            set => Value = (T[])value;
        }
    }

    class ResourceParameterPin<T> : ParameterPin, IVLPin<T> where T : class
    {
        public readonly ObjectParameterKey<T> Key;

        public ResourceParameterPin(ParameterCollection parameters, ObjectParameterKey<T> key) 
            : base(parameters, key)
        {
            this.Key = key;
        }

        public T Value
        {
            get => Parameters.Get(Key);
            set => Parameters.Set(Key, value ?? Key.DefaultValueMetadataT?.DefaultValue);
        }

        object IVLPin.Value
        {
            get => Value;
            set => Value = (T)value;
        }
    }

    abstract class Pin : IVLPin
    {
        public Pin(string name)
        {
            Name = name;
        }

        public abstract object Value { get; set; }

        public string Name { get; }
    }

    class Pin<T> : Pin, IVLPin<T>
    {
        public Pin(string name, T initialValue) : base(name)
        {
            Value = initialValue;
        }

        T IVLPin<T>.Value { get; set; }

        public sealed override object Value
        {
            get => ((IVLPin<T>)this).Value;
            set => ((IVLPin<T>)this).Value = (T)value;
        }
    }
}
