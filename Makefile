.PHONY: proto proto-go proto-csharp

proto: proto-go proto-csharp

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
