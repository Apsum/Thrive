using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
///   An abstract attribute for handling input methods.
///   Can be applied to a method.
/// </summary>
public abstract class InputAttribute : Attribute
{
    /// <summary>
    ///   All instances associated with this InputAttribute
    /// </summary>
    private readonly List<WeakReference> instances = new List<WeakReference>();

    /// <summary>
    ///   All references to instances pending removal
    /// </summary>
    private readonly List<WeakReference> disposed = new List<WeakReference>();

    /// <summary>
    ///   The method this Attribute is applied to
    /// </summary>
    public MethodBase Method { get; private set; }

    public override bool Equals(object obj)
    {
        if (!(obj is InputAttribute attr))
            return false;

        return Equals(attr.Method, Method);
    }

    public override int GetHashCode()
    {
        return Method != null ? Method.GetHashCode() : 0;
    }

    /// <summary>
    ///   Called by InputManager._Input()
    /// </summary>
    /// <param name="input">The event fired by the user</param>
    /// <returns>Returns whether the input was consumed or not</returns>
    public abstract bool OnInput(InputEvent input);

    /// <summary>
    ///   Called by InputManager._Process()
    /// </summary>
    /// <param name="delta">The time since the last call of OnProcess</param>
    public abstract void OnProcess(float delta);

    /// <summary>
    ///   Called when the games window lost it's focus.
    ///   Can be used to reset things to their normal state.
    /// </summary>
    public abstract void FocusLost();

    /// <summary>
    ///   Called by InputManager.LoadAttributes().
    ///   Sets the associated method.
    /// </summary>
    /// <param name="method">The method this attribute is associated with</param>
    internal void Init(MethodBase method)
    {
        Method = method;
    }

    /// <summary>
    ///   Called by InputManager.AddInstance().
    ///   Adds an instance to the list of associated instances.
    /// </summary>
    /// <param name="instance">The new instance</param>
    internal void AddInstance(WeakReference instance)
    {
        instances.Add(instance);
    }

    /// <summary>
    ///   Called by InputManager.RemoveInstance().
    ///   Removes an instance from the list of associated instances.
    /// </summary>
    /// <param name="instance">The instance to remove</param>
    internal void RemoveInstance(object instance)
    {
        instances.RemoveAll(p => !p.IsAlive || p.Target == instance);
    }

    /// <summary>
    ///   Call the associated method.
    ///   Calls the method with all of the instances or once if the method is static.
    /// </summary>
    /// <param name="parameters">The parameters the method will be called with</param>
    /// <returns>Returns whether the event was consumed or not</returns>
    protected bool CallMethod(params object[] parameters)
    {
        // Do nothing if no method is associated
        if (Method == null)
            return true;

        var result = false;
        Task.Run(() =>
        {
            lock (disposed)
            {
                disposed.Clear();
                if (Method.IsStatic)
                {
                    // Call the method without an instance if it's static
                    result = Method.Invoke(null, parameters) as bool? ?? true;
                }
                else
                {
                    // Call the method for each instance
                    instances.AsParallel().ForAll(p =>
                    {
                        if (!p.IsAlive)
                        {
                            // if the WeakReference got disposed
                            disposed.Add(p);
                            return;
                        }

                        var methodResult = Method.Invoke(p.Target, parameters) as bool? ?? true;
                        if (!result)
                            result = methodResult;
                    });
                }

                disposed.AsParallel().ForAll(p => instances.Remove(p));
            }
        });
        return result;
    }
}