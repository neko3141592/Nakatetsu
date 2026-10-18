.PHONY: proto proto-go proto-csharp proto-client diagram

proto: proto-go proto-csharp proto-client

proto-go:
	mkdir -p Server/gen/ctcv1
	protoc -I Protocol \
		--go_out=Server/gen/ctcv1 \
		--go_opt=paths=source_relative \
		--go-grpc_out=Server/gen/ctcv1 \
		--go-grpc_opt=paths=source_relative \
		Protocol/ctc.proto

proto-csharp:
	sh Tools/generate-csharp.sh

proto-client:
	protoc -I Protocol \
		--plugin=protoc-gen-es=client/node_modules/.bin/protoc-gen-es \
		--es_out=client/src/gen \
		--es_opt=target=ts \
		Protocol/ctc.proto

diagram:
	python3 Tools/export-circuit-diagram.py --all
