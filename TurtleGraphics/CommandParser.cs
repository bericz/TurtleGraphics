﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Flee.PublicTypes;

namespace TurtleGraphics {
	public class CommandParser {

		private static MainWindow win;

		internal static async Task<Queue<ParsedData>> Parse(string commands, MainWindow window) {
			string[] split = commands.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

			win = window;

			Queue<ParsedData> ret = new Queue<ParsedData>();
			StringReader reader = new StringReader(commands);
			Dictionary<string, object> vars = new Dictionary<string, object>() {
				{ "Width", win.DrawWidth },
				{ "Height", win.DrawHeight }
			};
			while (reader.Peek() != -1) {
				ParsedData data = await ParseLine(reader.ReadLine(), reader, vars);
				if (data != null) {
					ret.Enqueue(data);
				}
			}

			return ret;
		}


		private static async Task<ParsedData> ParseLine(string line, StringReader reader, Dictionary<string, object> variables) {
			if (string.IsNullOrWhiteSpace(line)) {
				return null;
			}
			string[] split = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

			switch (split[0]) {
				case "move": {
					ParsedData<Point> point = ParsePoint(split[1], variables);
					return new MoveParseData(win) {
						Exp = point.Exp,
						MoveTo = point.Value(),
						Value = point.Value,
						Variables = new Dictionary<string, object>(variables)
					};
				}
				case "for": {
					ForLoopData data = await ParseForLoop(split[1], reader, variables);
					data.Line = line;
					return data;
				}

				case "rotate": {
					ParsedData<double> data = ParseExpression<double>(split[1], variables);
					return new RotateParseData(win) {
						Angle = data.Value(),
						Variables = new Dictionary<string, object>(variables),
						Exp = data.Exp,
						Line = line,
						Value = data.Value
					};
				}

				case "fwd": {
					ParsedData<double> data = ParseExpression<double>(split[1], variables);
					return new ForwardParseData(win) {
						Distance = data.Value(),
						Variables = new Dictionary<string, object>(variables),
						Exp = data.Exp,
						Line = line,
						Value = data.Value
					};
				}

				default: {
					throw new InvalidOperationException();
				}
			}
		}


		private static async Task<ForLoopData> ParseForLoop(string v, StringReader reader, Dictionary<string, object> variables) {
			string[] split = v.Split();
			string variable = split[0];
			string[] range = split[2].Split(new[] { ".." }, StringSplitOptions.None);
			int from = int.Parse(range[0]);
			if (range[1].EndsWith("{")) {
				range[1] = range[1].Remove(range[1].Length - 1, 1);
			}
			variables.Add(variable, from);

			int to = int.Parse(range[1]);
			string next = reader.ReadLine();
			List<ParsedData> data = new List<ParsedData>();

			while (!next.StartsWith("}")) {
				ParsedData d = await ParseLine(next, reader, variables);
				data.Add(d);
				next = reader.ReadLine();
			}

			Queue<ParsedData> inner = new Queue<ParsedData>();

			for (int i = from + 1; i < to; i++) {
				variables[variable] = i;
				foreach (ParsedData dat in data) {
					ParsedData d = dat.Clone();
					d.Variables[variable] = i;
					inner.Enqueue(d);
				}
			}
			variables.Remove(variable);
			return new ForLoopData() { From = from, To = to, Var = variable, Queue = inner };
		}

		private static ParsedData<Point> ParsePoint(string v, Dictionary<string, object> variables) {
			string[] split = v.Split(',');
			if (v.StartsWith("(") && v.EndsWith(")")) {
				split[0] = split[0].Replace("(", "");
				split[1] = split[1].Replace(")", "");
			}

			ParsedData<double> X = ParseExpression<double>(split[0], variables);

			ParsedData<double> Y = ParseExpression<double>(split[1], variables);

			return new ParsedData<Point> { Line = v, Variables = new Dictionary<string, object>(variables), Value = () => new Point(X.Exp.Evaluate(), Y.Exp.Evaluate()) };

		}

		private static ParsedData<T> ParseExpression<T>(string v, Dictionary<string, object> variables) {
			ExpressionContext context = new ExpressionContext();

			foreach (KeyValuePair<string, object> item in variables) {
				context.Variables.Add(item.Key, item.Value);
			}

			IGenericExpression<T> result = context.CompileGeneric<T>(v);
			return new ParsedData<T> { Line = v, Variables = new Dictionary<string, object>(variables), Exp = result, Value = result.Evaluate };
		}
	}
}