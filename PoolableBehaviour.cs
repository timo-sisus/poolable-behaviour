using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Sisus.Pool
{
	/// <summary>
	/// Base class for components that can be reused using the Object Pool pattern
 	/// to avoid garbage being generated when instances are spawned.
	/// <para>
	/// Use <see cref="PoolableBehaviour{TPoolableBehaviour}.Instantiate"/> to spawn instances
	/// and <see cref="PoolableBehaviour{TPoolableBehaviour}.Dispose"/> to despawn them to the object pool.
	/// </para>
	/// <para>
	/// If the <see cref="PoolableBehaviour{TPoolableBehaviour}.OnUpdate"/> method is overridden, it will be executed during every Update event.
	/// </para>
	/// <para>
	/// If the <see cref="PoolableBehaviour{TPoolableBehaviour}.FixedUpdate"/> method is overridden, it will be executed during every FixedUpdate event.
	/// </para>
	/// <para>
	/// If the <see cref="PoolableBehaviour{TPoolableBehaviour}.LateUpdate"/> method is overridden, it will be executed during every LateUpdae event.
	/// </para>
	/// </summary>
	/// <typeparam name="TPoolableBehaviour"> The concrete type of the component that uses the Object Pool pattern. </typeparam>
	public abstract class PoolableBehaviour<TPoolableBehaviour> : MonoBehaviour, IDisposable where TPoolableBehaviour : PoolableBehaviour<TPoolableBehaviour>
	{
		static readonly TPoolableBehaviour[] inactiveInstances;
		static readonly TPoolableBehaviour[] activeInstances;

		static readonly int maxCount;
		
		static bool hasBeenInitialized;
		static int inactiveCount;
		static int activeCount;

		static PoolableBehaviour()
		{
			maxCount = typeof(TPoolableBehaviour).GetCustomAttribute<ObjectPoolSettingsAttribute>() is ObjectPoolSettingsAttribute settings
				? settings.MaxSize
				: ObjectPoolSettingsAttribute.DEFAULT_MAX_SIZE;

			inactiveInstances = new TPoolableBehaviour[maxCount];
			activeInstances = new TPoolableBehaviour[maxCount];
		}

		#if UNITY_EDITOR
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetStaticState()
		{
			Array.Clear(inactiveInstances, 0, inactiveCount);
			Array.Clear(activeInstances, 0, activeCount);
			inactiveCount = 0;
			activeCount = 0;
			hasBeenInitialized = false;
		}
		#endif

		/// <summary>
		/// Is this instance currently active or not?
		/// <para>
		/// Inactive instances are ones that are currently in the object pool or that have been destroyed.
		/// </summary>
		public bool IsActive { get; private set; } = true;

		/// <summary>
		/// Returns an instance from the object pool, if it contains any; otherwise, creates a new instance and returns it.
		/// </summary>
		/// <returns> An instance of type <see cref="TPoolableBehaviour"/>. </returns>
		public TPoolableBehaviour Instantiate()
		{
			TPoolableBehaviour result;

			if(inactiveCount == 0)
			{
				if(hasBeenInitialized)
				{
					return CreateNewActiveInstance(this as TPoolableBehaviour);
				}

				hasBeenInitialized = true;

				CreateInitialInstances(this as TPoolableBehaviour);

				StartUpdatingActiveInstances();

				if(inactiveCount == 0)
				{
					return CreateNewActiveInstance(this as TPoolableBehaviour);
				}
			}

			inactiveCount--;
			result = inactiveInstances[inactiveCount];
			inactiveInstances[inactiveCount] = null;
			activeInstances[activeCount] = result;
			activeCount++;
			result.IsActive = true;
			result.OnStart();
			return result;

			static TPoolableBehaviour CreateNewActiveInstance(TPoolableBehaviour prefab)
			{
				activeCount++;
				var result = Instantiate(prefab);
				activeInstances[activeCount - 1] = result;
				return result;
			}

			static void CreateInitialInstances(TPoolableBehaviour prefab)
			{
				int initialSize;
				if(typeof(TPoolableBehaviour).GetCustomAttribute<ObjectPoolSettingsAttribute>() is ObjectPoolSettingsAttribute settings)
				{
					initialSize = settings.InitialSize;
				}
				else
				{
					initialSize = ObjectPoolSettingsAttribute.DEFAULT_INITIAL_SIZE;
				}

				while(inactiveCount < initialSize)
				{
					var instance = Instantiate(prefab);
					inactiveInstances[inactiveCount] = instance;
					instance.OnDispose();
					inactiveCount++;
				}
			}

			static void CreateInactiveInstance(TPoolableBehaviour prefab)
			{
				var instance = Instantiate(prefab);
				inactiveInstances[inactiveCount] = instance;
				instance.OnDispose();
				inactiveCount++;
			}

			static void StartUpdatingActiveInstances()
			{
				var rootSystem  = PlayerLoop.GetCurrentPlayerLoop();
				var subSystems = rootSystem.subSystemList;
				for (int i = 0; i < subSystems.Length; i++)
				{
					var subSystem = subSystems[i];
					if(subSystem.type == typeof(Update))
					{
						if(HasMethodBeenOverriden(nameof(OnUpdate)))
						{
							subSystem.updateDelegate += UpdateAllActiveInstances;
							subSystems[i] = subSystem;
						}

						continue;
					}
					
					if(subSystem.type == typeof(FixedUpdate))
					{
						if(HasMethodBeenOverriden(nameof(OnFixedUpdate)))
						{
							subSystem.updateDelegate += FixedUpdateAllActiveInstances;
							subSystems[i] = subSystem;
						}

						continue;
					}
					
					if(subSystem.type == typeof(PostLateUpdate))
					{
						if(HasMethodBeenOverriden(nameof(OnLateUpdate)))
						{
							subSystem.updateDelegate += LateUpdateAllActiveInstances;
							subSystems[i] = subSystem;
						}
					}
				}

				rootSystem.subSystemList = subSystems; 
				PlayerLoop.SetPlayerLoop(rootSystem);
			}

			static void UpdateAllActiveInstances()
			{
				for(int i = 0; i < activeCount; i++)
				{
					activeInstances[i].OnUpdate();
				}
			}

			static void FixedUpdateAllActiveInstances()
			{
				for(int i = 0; i < activeCount; i++)
				{
					activeInstances[i].OnFixedUpdate();
				}
			}

			static void LateUpdateAllActiveInstances()
			{
				for(int i = 0; i < activeCount; i++)
				{
					activeInstances[i].OnLateUpdate();
				}
			}

			static bool HasMethodBeenOverriden(string methodName)
			{
				for(var type = typeof(TPoolableBehaviour); type != typeof(PoolableBehaviour<TPoolableBehaviour>); type = type.BaseType)
				{
					if(type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly) is not null)
					{
						return true;
					}
				}
				
				return false;
			}
		}

		/// <summary>
		/// Disposes the instance into the object pool, for later reuse.
		/// </summary>
		public void Dispose()
		{
			if(!IsActive)
			{
				return;
			}

			IsActive = false;
			activeCount--;
			activeInstances[activeCount] = null;

			if(inactiveCount < maxCount)
			{
				inactiveInstances[inactiveCount] = this as TPoolableBehaviour;
				inactiveCount++;
				OnDispose();
			}
			else
			{
				Destroy(this);
			}
		}

		/// <summary>
		/// Called every time that this instance becomes active.
		/// </summary>
		protected virtual void OnStart() { }

		/// <summary>
		/// This method gets called during every Update event if overridden.
		/// </summary>
		protected virtual void OnUpdate() { }

		/// <summary>
		/// This method gets called during every FixedUpdate event if overridden.
		/// </summary>
		protected virtual void OnFixedUpdate() { }

		/// <summary>
		/// This method gets called during every LateUpdate event if overridden.
		/// </summary>
		protected virtual void OnLateUpdate() { }

		/// <summary>
		/// Called every time that this instance is disposed to the object pool, and when the instance is destroyed.
		/// </summary>
		protected virtual void OnDispose() { }

		private protected void Start() => OnStart();

		private protected void OnDestroy()
		{
			if(IsActive)
			{
				IsActive = false;
				OnDispose();
				RemoveDestroyedInstance(activeInstances, this as TPoolableBehaviour, ref activeCount);
			}
			else
			{
				RemoveDestroyedInstance(inactiveInstances, this as TPoolableBehaviour, ref inactiveCount);
			}

			static void RemoveDestroyedInstance(TPoolableBehaviour[] instances, TPoolableBehaviour instance, ref int instanceCount)
			{
				int index = Array.IndexOf(instances, instance);
				if(index == -1)
				{
					return;
				}

				instanceCount--;

				for(int i = index; i < instanceCount; i++)
				{
					instances[i] = instances[i + 1];
				}

				instances[instanceCount] = null;
			}
		}
	}
}
