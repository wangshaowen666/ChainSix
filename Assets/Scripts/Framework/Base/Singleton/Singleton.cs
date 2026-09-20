// ﻿/*--------------------------------------------------------------
//  * File: Singleton.cs
//  * Author: Wsw
//  * Feedback: 614270423@qq.com
//  * Time: 2024/03/14 15:52:25 
//  * Copyright: Copyright © 2024 wangshaowen. All rights reserved.
//  *--------------------------------------------------------------
//  */
//  ****采用管理类+静态类的方式，整个项目不使用单例
// using System;
// using System.Reflection;
// using UnityEngine;
//
// // 单例应该私有化构造，所以T没加new约束，通过反射实例化
// public class Singleton<T> where T:Singleton<T>
// {
//     private static T instance;
//     private static readonly object lockObject = new object();
//     private static bool isDisposed = false;
//     
//     public static bool IsDisposed => isDisposed;
//     public static bool IsCreated => instance != null;
//
//     public static T Instance
//     {
//         get
//         {
//             if (instance != null)
//             {
//                 return instance;
//             }
//             
//             lock (lockObject)
//             {
//                 if (isDisposed)
//                 {
//                     throw new Exception(typeof(T).Name + "单例已被释放，无法再次访问");
//                 }
//                 
//                 if (instance == null)
//                 {
//                     var ctors = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
//                     var ctor = Array.Find(ctors, c => c.GetParameters().Length == 0);
//
//                     if (ctor == null)
//                         throw new Exception(typeof(T).Name + "缺少无参的私有构造函数，请私有化构造");
//                     instance = ctor.Invoke(null) as T;
//                     if (instance != null) 
//                         instance.OnInit();
//                 }
//             }
//             return instance;
//         }
//     }
//     
//     protected virtual void OnInit() { }
//     protected virtual void OnDispose() { }
//     public static void Dispose()
//     {
//         lock (lockObject)
//         {
//             if (instance != null)
//             {
//                 instance.OnDispose();
//                 instance = null;
//             }
//             isDisposed = true;
//         }
//     }
//     public static void Reset()
//     {
//         lock (lockObject)
//         {
//             if (instance != null)
//             {
//                 instance.OnDispose();
//                 instance = null;
//             }
//             isDisposed = false;
//         }
//     }
// }
//
