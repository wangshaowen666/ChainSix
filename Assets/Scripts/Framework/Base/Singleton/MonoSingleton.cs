// ﻿/*--------------------------------------------------------------
//  * File: MonoSingleton.cs
//  * Author: Wsw
//  * Feedback: 614270423@qq.com
//  * Time: 2024/03/14 17:30:34 
//  * Copyright: Copyright © 2024 wangshaowen. All rights reserved.
//  *--------------------------------------------------------------
//  */
//  ****采用管理类+静态类的方式，整个项目不使用单例
// using System;
// using UnityEngine;
//
// public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
// {
//     private static T _instance;
//     private static readonly object _lockObject = new object();
//     private static bool _isDisposed = false;
//     private static bool _isInitialized = false;
//     
//     public static T Instance  
//     {
//         get  
//         {
//             lock (_lockObject)
//             {
//                 if (_isDisposed)
//                 {
//                     Debug.LogError($"{typeof(T).Name} 单例已被释放，无法再次访问");
//                     return null;
//                 }
//                 
//                 if (_instance == null)  
//                 {
//                     _instance = FindObjectOfType<T>();  
//                     if (_instance == null)  
//                     {
//                         GameObject singletonObject = new GameObject();  
//                         _instance = singletonObject.AddComponent<T>();
//                         singletonObject.name = typeof(T).Name;
//                         DontDestroyOnLoad(singletonObject);
//                         
//                         // 调用初始化方法
//                         (_instance as MonoSingleton<T>).OnInit();
//                         _isInitialized = true;
//                     }
//                     else
//                     {
//                         // 如果实例已经存在但未初始化，调用初始化方法
//                         if (!_isInitialized)
//                         {
//                             (_instance as MonoSingleton<T>).OnInit();
//                             _isInitialized = true;
//                         }
//                     }
//                 }
//                 
//                 return _instance;
//             }
//         }
//     }
//     
//     /// <summary>
//     /// 单例是否已创建
//     /// </summary>
//     public static bool IsCreated => _instance != null;
//     
//     /// <summary>
//     /// 单例是否已释放
//     /// </summary>
//     public static bool IsDisposed => _isDisposed;
//     
//     /// <summary>
//     /// 初始化方法，在单例实例化时调用
//     /// </summary>
//     protected virtual void OnInit() { }
//     
//     /// <summary>
//     /// 释放资源方法，在单例被释放时调用
//     /// </summary>
//     protected virtual void OnDispose() { }
//     
//     /// <summary>
//     /// 释放单例资源
//     /// </summary>
//     public static void Dispose()
//     {
//         lock (_lockObject)
//         {
//             if (_instance != null)
//             {
//                 // 调用释放方法
//                 (_instance as MonoSingleton<T>).OnDispose();
//                 
//                 // 销毁GameObject
//                 GameObject obj = _instance.gameObject;
//                 if (obj != null)
//                 {
//                     Destroy(obj);
//                 }
//                 
//                 _instance = null;
//                 _isInitialized = false;
//                 _isDisposed = true;
//             }
//         }
//     }
//     
//     /// <summary>
//     /// 重置单例状态，允许重新创建实例
//     /// </summary>
//     public static void Reset()
//     {
//         lock (_lockObject)
//         {
//             if (_instance != null)
//             {
//                 // 调用释放方法
//                 (_instance as MonoSingleton<T>).OnDispose();
//                 
//                 // 销毁GameObject
//                 GameObject obj = _instance.gameObject;
//                 if (obj != null)
//                 {
//                     Destroy(obj);
//                 }
//                 
//                 _instance = null;
//                 _isInitialized = false;
//             }
//             
//             _isDisposed = false;
//         }
//     }
//   
//     protected virtual void Awake()  
//     {
//         lock (_lockObject)
//         {
//             if (_instance == null)  
//             {
//                 _instance = this as T;
//                 DontDestroyOnLoad(gameObject);
//                 
//                 // 调用初始化方法
//                 OnInit();
//                 _isInitialized = true;
//             }
//             // 如果单例是直接挂载于场景中，切换场景又切换回来后，场景中会多一个，需要删除。
//             // 不删除也不会出问题，_instance已被赋值，Instance拿到的始终会是第一个，只是稍微有点奇怪
//             else  
//             {
//                 Destroy(gameObject);
//             }
//         }
//     }
//     
//     protected virtual void OnDestroy()
//     {
//         lock (_lockObject)
//         {
//             if (_instance == this)
//             {
//                 // 调用释放方法
//                 OnDispose();
//                 
//                 _instance = null;
//                 _isInitialized = false;
//                 _isDisposed = false; // 允许重新创建实例
//             }
//         }
//     }
// }