// Copyright (c) rubicon IT GmbH, www.rubicon.eu
//
// See the NOTICE file distributed with this work for additional information
// regarding copyright ownership.  rubicon licenses this file to you under 
// the Apache License, Version 2.0 (the "License"); you may not use this 
// file except in compliance with the License.  You may obtain a copy of the 
// License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.  See the 
// License for the specific language governing permissions and limitations
// under the License.
// 
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.ExpressionTreeVisitors;
using Remotion.Linq.Utilities;

namespace Remotion.Linq.Parsing.Structure.IntermediateModel
{
	/// <summary>
	/// Represents a <see cref="MethodCallExpression"/> for 
	/// <see cref="Queryable.SelectMany{TSource,TCollection,TResult}(System.Linq.IQueryable{TSource},System.Linq.Expressions.Expression{System.Func{TSource,System.Collections.Generic.IEnumerable{TCollection}}},System.Linq.Expressions.Expression{System.Func{TSource,TCollection,TResult}})"/>.
	/// It is generated by <see cref="ExpressionTreeParser"/> when an <see cref="Expression"/> tree is parsed.
	/// This node represents an additional query source introduced to the query.
	/// </summary>
	public class SelectManyExpressionNode : MethodCallExpressionNodeBase, IQuerySourceExpressionNode
	{
		public static readonly MethodInfo[] SupportedMethods =
			new[]
			{
				GetSupportedMethod (() => Queryable.SelectMany<object, object[], object> (null, o => null, null)),
				GetSupportedMethod (() => Enumerable.SelectMany<object, object[], object> (null, o => null, null)),
				GetSupportedMethod (() => Queryable.SelectMany<object, object[]> (null, o => null)),
				GetSupportedMethod (() => Enumerable.SelectMany<object, object[]> (null, o => null)),
			};

		private readonly ResolvedExpressionCache<Expression> _cachedCollectionSelector;
		private readonly ResolvedExpressionCache<Expression> _cachedResultSelector;

		public SelectManyExpressionNode(
			MethodCallExpressionParseInfo parseInfo, LambdaExpression collectionSelector, LambdaExpression resultSelector)
			: base(parseInfo)
		{
			if (collectionSelector.Parameters.Count != 1)
				throw new ArgumentException("Collection selector must have exactly one parameter.", "collectionSelector");

			CollectionSelector = collectionSelector;

			if (resultSelector != null)
			{
				if (resultSelector.Parameters.Count != 2)
					throw new ArgumentException("Result selector must have exactly two parameters.", "resultSelector");

				ResultSelector = resultSelector;
			}
			else
			{
				var parameter1 = Expression.Parameter(collectionSelector.Parameters[0].Type, collectionSelector.Parameters[0].Name);
				var itemType = ReflectionUtility.GetItemTypeOfClosedGenericIEnumerable(CollectionSelector.Body.Type, "collectionSelector");
				var parameter2 = Expression.Parameter(itemType, parseInfo.AssociatedIdentifier);
				ResultSelector = Expression.Lambda(parameter2, parameter1, parameter2);
			}

			_cachedCollectionSelector = new ResolvedExpressionCache<Expression>(this);
			_cachedResultSelector = new ResolvedExpressionCache<Expression>(this);
		}

		public LambdaExpression CollectionSelector { get; private set; }
		public LambdaExpression ResultSelector { get; private set; }

		public Expression GetResolvedCollectionSelector(ClauseGenerationContext clauseGenerationContext)
		{
			return _cachedCollectionSelector.GetOrCreate(
				r => r.GetResolvedExpression(CollectionSelector.Body, CollectionSelector.Parameters[0], clauseGenerationContext));
		}

		public Expression GetResolvedResultSelector(ClauseGenerationContext clauseGenerationContext)
		{
			// our result selector usually looks like this: (i, j) => new { i = i, j = j }
			// with the data for i coming from the previous node and j identifying the data from this node

			// we resolve the selector by first substituting j by a QuerySourceReferenceExpression pointing back to us, before asking the previous node 
			// to resolve i

			return _cachedResultSelector.GetOrCreate(
				r => r.GetResolvedExpression(
						 QuerySourceExpressionNodeUtility.ReplaceParameterWithReference(this, ResultSelector.Parameters[1], ResultSelector.Body, clauseGenerationContext),
						 ResultSelector.Parameters[0],
						 clauseGenerationContext));
		}

		public override Expression Resolve(
			ParameterExpression inputParameter, Expression expressionToBeResolved, ClauseGenerationContext clauseGenerationContext)
		{
			// we modify the structure of the stream of data coming into this node by our result selector,
			// so we first resolve the result selector, then we substitute the result for the inputParameter in the expressionToBeResolved
			var resolvedResultSelector = GetResolvedResultSelector(clauseGenerationContext);
			return ReplacingExpressionTreeVisitor.Replace(inputParameter, resolvedResultSelector, expressionToBeResolved);
		}

		protected override QueryModel ApplyNodeSpecificSemantics(QueryModel queryModel, ClauseGenerationContext clauseGenerationContext)
		{
			var resolvedCollectionSelector = GetResolvedCollectionSelector(clauseGenerationContext);
			var clause = new AdditionalFromClause(ResultSelector.Parameters[1].Name, ResultSelector.Parameters[1].Type, resolvedCollectionSelector);
			queryModel.BodyClauses.Add(clause);

			clauseGenerationContext.AddContextInfo(this, clause);

			var selectClause = queryModel.SelectClause;
			selectClause.Selector = GetResolvedResultSelector(clauseGenerationContext);

			return queryModel;
		}
	}
}
